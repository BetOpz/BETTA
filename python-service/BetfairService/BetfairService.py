# In python-service/BetfairService/BetfairService.py

from datetime import datetime, timedelta
from flask import Flask, request, jsonify
from flask_cors import CORS
import betfairlightweight
import logging
import threading
import time

app = Flask(__name__)
CORS(app)
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

trading_client = None
session_token = None
keep_alive_thread = None
keep_alive_active = False

def start_keep_alive():
    global keep_alive_thread, keep_alive_active
    keep_alive_active = True
    def worker():
        while keep_alive_active:
            try:
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
    data = request.json or {}
    username = data.get('username')
    password = data.get('password')
    app_key  = data.get('app_key')
    if not all([username, password, app_key]):
        return jsonify({'success': False, 'error': 'Missing credentials'}), 400

    try:
        trading_client = betfairlightweight.APIClient(username=username, password=password, app_key=app_key)
        trading_client.login_interactive()
        token = getattr(trading_client, 'session_token', None)
        if not token:
            return jsonify({'success': False, 'error': 'No token returned'}), 401
        session_token = token
        start_keep_alive()
        return jsonify({'success': True, 'session_token': session_token})
    except Exception as e:
        logger.exception("Login error")
        return jsonify({'success': False, 'error': str(e)}), 500

@app.route('/data/horse-markets', methods=['GET'])
def horse_markets():
    """
    Returns all Horse Racing markets in Great Britain and Ireland
    starting within the next 24 hours.
    """
    if not trading_client:
        return jsonify({'success': False, 'error': 'Not logged in'}), 401

    now = datetime.utcnow()
    in_24h = now + timedelta(hours=24)

    mf = betfairlightweight.filters.market_filter(
        event_type_ids=[7],                 # Horse Racing
        market_countries=['GB', 'IE'],      # Correct filter key
        market_start_time={
            'from': now.strftime('%Y-%m-%dT%H:%M:%SZ'),
            'to':   in_24h.strftime('%Y-%m-%dT%H:%M:%SZ')
        }
    )

    try:
        catalogue = trading_client.betting.list_market_catalogue(
            filter=mf,
            max_results=200,
            market_projection=['MARKET_START_TIME']
        )
        markets = [{
            'market_id':     mc.market_id,
            'market_name':   mc.market_name,
            'start_time':    mc.market_start_time.isoformat(),
            'total_matched': mc.total_matched
        } for mc in catalogue]

        return jsonify({'success': True, 'markets': markets})
    except Exception as e:
        return jsonify({'success': False, 'error': str(e)}), 500

if __name__ == '__main__':
    logger.info("Starting BETTA Betfair Service...")
    app.run(host='127.0.0.1', port=5000, debug=True)
