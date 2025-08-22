# In python-service/BetfairService/BetfairService.py

from datetime import datetime, timedelta
from flask import Flask, request, jsonify
from flask_cors import CORS
import betfairlightweight
import logging
import threading
import time
import pytz

app = Flask(__name__)
CORS(app)
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

trading_client = None
session_token = None
keep_alive_thread = None
keep_alive_active = False

# UK timezone handling
UK_TZ = pytz.timezone('Europe/London')

def convert_to_uk_time(utc_datetime):
    """Convert UTC datetime to UK time (handles BST/GMT automatically)"""
    if utc_datetime.tzinfo is None:
        utc_datetime = pytz.utc.localize(utc_datetime)
    return utc_datetime.astimezone(UK_TZ)

def get_first_price(offers):
    """Safely get the first price from an offers list"""
    return offers[0].price if offers and len(offers) > 0 else None

def map_race_status(race_status):
    """Map Betfair race status to display format"""
    if not race_status:
        return "DORMANT"
    
    # These are the actual Betfair race statuses
    status_map = {
        'DORMANT': 'DORMANT',
        'DELAYED': 'DELAYED', 
        'PARADING': 'PARADING',
        'GOINGDOWN': 'GOING DOWN',
        'GOINGBEHIND': 'GOING BEHIND',
        'ATTHEPOST': 'AT THE POST',
        'STARTED': 'STARTED',
        'FINISHED': 'FINISHED',
        'FALSESTART': 'FALSE START',
        'PHOTOGRAPH': 'PHOTOGRAPH',
        'RESULT': 'RESULT',
        'WEIGHEDIN': 'WEIGHED IN',
        'RACEVOID': 'RACE VOID',
        'ABANDONED': 'ABANDONED'
    }
    
    return status_map.get(race_status.upper(), race_status)

def start_keep_alive():
    global keep_alive_thread, keep_alive_active
    keep_alive_active = True
    def worker():
        while keep_alive_active:
            try:
                if trading_client:
                    trading_client.keep_alive()
                    logger.info("Keep-alive successful")
                time.sleep(900)
            except Exception as e:
                logger.error(f"Keep-alive error: {e}")
                time.sleep(60)
    keep_alive_thread = threading.Thread(target=worker, daemon=True)
    keep_alive_thread.start()

@app.route('/login', methods=['POST'])
def login():
    global trading_client, session_token
    try:
        data = request.json or {}
        username = data.get('username')
        password = data.get('password')
        app_key  = data.get('app_key')
        
        if not all([username, password, app_key]):
            return jsonify({'success': False, 'error': 'Missing credentials'})

        trading_client = betfairlightweight.APIClient(username=username, password=password, app_key=app_key)
        trading_client.login_interactive()
        token = getattr(trading_client, 'session_token', None)
        
        if not token:
            return jsonify({'success': False, 'error': 'No token returned'})
            
        session_token = token
        start_keep_alive()
        return jsonify({'success': True, 'session_token': session_token})
        
    except Exception as e:
        logger.exception("Login error")
        return jsonify({'success': False, 'error': str(e)})

@app.route('/data/horse-markets', methods=['GET'])
def horse_markets():
    """
    Returns Horse Racing WIN markets with actual Betfair race status.
    """
    try:
        if not trading_client:
            return jsonify({'success': False, 'error': 'Not logged in'})

        now = datetime.utcnow()
        uk_now = convert_to_uk_time(now)
        in_24h = now + timedelta(hours=24)

        mf = betfairlightweight.filters.market_filter(
            event_type_ids=[7],
            market_countries=['GB', 'IE'],
            market_type_codes=['WIN'],
            market_start_time={
                'from': now.strftime('%Y-%m-%dT%H:%M:%SZ'),
                'to':   in_24h.strftime('%Y-%m-%dT%H:%M:%SZ')
            }
        )

        # Get market catalogue with ALL projections to get race status
        catalogue = trading_client.betting.list_market_catalogue(
            filter=mf,
            max_results=200,
            market_projection=['MARKET_START_TIME', 'EVENT', 'RUNNER_DESCRIPTION', 'MARKET_DESCRIPTION']
        )
        
        if not catalogue:
            return jsonify({
                'success': True, 
                'markets': [],
                'current_time_uk': uk_now.strftime('%d %b %Y %H:%M:%S'),
                'server_time_utc': now.strftime('%Y-%m-%dT%H:%M:%SZ')
            })

        # Get market books to fetch market status AND race status
        market_ids = [mc.market_id for mc in catalogue]
        
        try:
            market_books = trading_client.betting.list_market_book(
                market_ids=market_ids,
                price_projection={'priceData': []}
            )
            # Create lookups for both market status and race status
            market_status_lookup = {book.market_id: book.status for book in market_books}
            race_status_lookup = {}
            
            # Try to extract race status from market books (if available)
            for book in market_books:
                # Race status might be in different fields depending on API version
                race_status = None
                if hasattr(book, 'race_status'):
                    race_status = book.race_status
                elif hasattr(book, 'raceStatus'):
                    race_status = book.raceStatus
                elif hasattr(book, 'status_info'):
                    race_status = getattr(book.status_info, 'race_status', None)
                
                race_status_lookup[book.market_id] = race_status
                
        except Exception as e:
            logger.error(f"Error fetching market books: {e}")
            market_status_lookup = {}
            race_status_lookup = {}
        
        # Get unique venues and assign rotating colors (0-14)
        unique_venues = list(set(mc.event.venue for mc in catalogue))
        venue_colors = {venue: i % 15 for i, venue in enumerate(sorted(unique_venues))}
        
        markets = []
        for mc in catalogue:
            try:
                # Convert UTC to UK time
                uk_time = convert_to_uk_time(mc.market_start_time)
                formatted_time = uk_time.strftime('%d %b %H:%M')
                
                # Calculate time to start in minutes
                time_to_start = (mc.market_start_time - now).total_seconds() / 60
                
                # Get race status (preferred) or fallback to market status
                race_status = race_status_lookup.get(mc.market_id)
                market_status = market_status_lookup.get(mc.market_id, 'OPEN')
                
                if race_status:
                    # Use actual Betfair race status
                    display_status = map_race_status(race_status)
                else:
                    # Fallback to time-based status if no race status available
                    if market_status == 'CLOSED':
                        display_status = 'FINISHED'
                    elif market_status == 'SUSPENDED':
                        display_status = 'SUSPENDED'
                    elif time_to_start <= 0:
                        display_status = 'STARTED'
                    elif time_to_start <= 2:
                        display_status = 'AT THE POST'
                    elif time_to_start <= 5:
                        display_status = 'GOING DOWN'
                    elif time_to_start <= 10:
                        display_status = 'PARADING'
                    else:
                        display_status = 'DORMANT'
                
                # Format display with status
                combined_display = f"{formatted_time}  {mc.event.venue} - {mc.market_name}"
                status_display = f"[{display_status}] {combined_display}"
                
                # Color coding based on race status
                if display_status in ['FINISHED', 'RESULT', 'WEIGHED IN']:
                    status_color = 'gray'
                elif display_status in ['STARTED', 'PHOTOGRAPH']:
                    status_color = 'red'
                elif display_status in ['AT THE POST', 'GOING BEHIND']:
                    status_color = 'orange'
                elif display_status in ['GOING DOWN', 'PARADING']:
                    status_color = 'yellow'
                elif display_status in ['DELAYED', 'SUSPENDED']:
                    status_color = 'purple'
                else:
                    status_color = 'green'
                
                markets.append({
                    'race_info': combined_display,
                    'race_info_with_status': status_display,
                    'venue': mc.event.venue or "Unknown",
                    'color_index': venue_colors.get(mc.event.venue, 0),
                    'market_id': mc.market_id,
                    'market_name': mc.market_name or "Race",
                    'start_time': formatted_time,
                    'event_name': getattr(mc.event, 'name', mc.event.venue) if hasattr(mc.event, 'name') else mc.event.venue,
                    'time_to_start_minutes': round(time_to_start, 1),
                    'race_status': display_status,
                    'market_status': market_status,
                    'raw_race_status': race_status,
                    'status_color': status_color
                })
            except Exception as e:
                logger.error(f"Error processing market {mc.market_id}: {e}")
                continue

        # Sort by time to start (soonest first)
        markets.sort(key=lambda x: x['time_to_start_minutes'])

        return jsonify({
            'success': True, 
            'markets': markets,
            'current_time_uk': uk_now.strftime('%d %b %Y %H:%M:%S'),
            'server_time_utc': now.strftime('%Y-%m-%dT%H:%M:%SZ')
        })
        
    except Exception as e:
        logger.error(f"Error in horse_markets: {str(e)}")
        return jsonify({'success': False, 'error': f'Server error: {str(e)}'})

@app.route('/data/market-details/<market_id>', methods=['GET'])
def market_details(market_id):
    """
    Returns detailed market data with proper handling of removed runners (non-runners).
    """
    try:
        if not trading_client:
            return jsonify({'success': False, 'error': 'Not logged in'})

        # Get market book with prices
        market_book = trading_client.betting.list_market_book(
            market_ids=[market_id],
            price_projection={
                'priceData': ['EX_BEST_OFFERS'],
                'exBestOffersOverrides': {'bestPricesDepth': 3}
            }
        )
        
        if not market_book:
            return jsonify({'success': False, 'error': 'Market not found'})
            
        book = market_book[0]
        
        # Check market status
        market_status = book.status
        if market_status == 'CLOSED':
            return jsonify({
                'success': False, 
                'error': 'This race has finished.',
                'market_status': 'CLOSED',
                'user_message': 'Race Finished - Results available on Betfair'
            })
        elif market_status == 'SUSPENDED':
            return jsonify({
                'success': False,
                'error': 'This market is currently suspended.',
                'market_status': 'SUSPENDED', 
                'user_message': 'Market Suspended - Please try again later'
            })
        
        # Get market catalogue for runner names
        catalogue = trading_client.betting.list_market_catalogue(
            filter={'marketIds': [market_id]},
            market_projection=['RUNNER_DESCRIPTION', 'MARKET_START_TIME', 'EVENT']
        )
        
        if not catalogue:
            return jsonify({'success': False, 'error': 'Market catalogue not found'})
            
        cat = catalogue[0]
        
        # Convert start time to UK time
        uk_start_time = convert_to_uk_time(cat.market_start_time)
        formatted_start_time = uk_start_time.strftime('%d %b %H:%M')
        
        # Check if race has started (in-play)
        now = datetime.utcnow()
        race_started = now >= cat.market_start_time
        
        # Build runner data with safe handling of removed runners
        runners = []
        removed_runners = []
        
        for runner in book.runners:
            try:
                # Check if runner is removed/non-runner
                runner_status = getattr(runner, 'status', 'ACTIVE')
                if runner_status.upper() == 'REMOVED':
                    # Handle removed runners separately
                    runner_desc = next((r for r in cat.runners if r.selection_id == runner.selection_id), None)
                    removed_runners.append({
                        'selection_id': runner.selection_id,
                        'name': f"NR - {runner_desc.runner_name if runner_desc else f'Runner {runner.selection_id}'}",
                        'status': 'REMOVED'
                    })
                    continue
                
                # Process active runners normally
                runner_desc = next((r for r in cat.runners if r.selection_id == runner.selection_id), None)
                
                # Safely get prices - handle missing offers
                back_price = None
                lay_price = None
                
                if hasattr(runner, 'ex') and runner.ex:
                    back_price = get_first_price(runner.ex.available_to_back)
                    lay_price = get_first_price(runner.ex.available_to_lay)
                    
                runners.append({
                    'selection_id': runner.selection_id,
                    'name': runner_desc.runner_name if runner_desc else f"Runner {runner.selection_id}",
                    'back_price': back_price,
                    'lay_price': lay_price,
                    'status': runner_status,
                    'last_price_traded': getattr(runner, 'last_price_traded', None),
                    'total_matched': getattr(runner, 'total_matched', 0)
                })
            except Exception as e:
                logger.error(f"Error processing runner {runner.selection_id}: {e}")
                continue
        
        # Get proper race status display
        time_to_start = (cat.market_start_time - now).total_seconds() / 60
        if race_started:
            race_status_display = "STARTED"
        elif time_to_start <= 2:
            race_status_display = "AT THE POST"
        elif time_to_start <= 10:
            race_status_display = "PARADING"
        else:
            race_status_display = "OPEN"
            
        status_message = f" • {race_status_display}"
        
        # Add non-runner info to status if any
        if removed_runners:
            status_message += f" • {len(removed_runners)} Non-Runner{'s' if len(removed_runners) > 1 else ''}"
        
        return jsonify({
            'success': True,
            'market_id': market_id,
            'market_name': cat.market_name or "Race",
            'venue': cat.event.venue or "Unknown",
            'start_time': formatted_start_time,
            'event_name': getattr(cat.event, 'name', cat.event.venue) if hasattr(cat.event, 'name') else cat.event.venue,
            'status': market_status,
            'race_status': race_status_display,
            'in_play': race_started,
            'status_message': status_message,
            'total_matched': getattr(book, 'total_matched', 0),
            'runners': sorted(runners, key=lambda x: x['back_price'] or 999 if x['back_price'] else 999),
            'removed_runners': removed_runners,
            'non_runner_count': len(removed_runners)
        })
        
    except Exception as e:
        logger.error(f"Error in market_details: {str(e)}")
        return jsonify({'success': False, 'error': f'Server error: {str(e)}'})

if __name__ == '__main__':
    logger.info("Starting BETTA Betfair Service...")
    app.run(host='127.0.0.1', port=5000, debug=True)
