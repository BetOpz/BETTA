from flask import Flask, request, jsonify
from flask_cors import CORS
import betfairlightweight
import logging
import threading
import time

app = Flask(__name__)
CORS(app)

trading_client = None
session_token = None
keep_alive_thread = None
keep_alive_active = False

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

@app.route('/health', methods=['GET'])
def health_check():
    return jsonify({'status': 'healthy', 'service': 'BETTA Betfair Service'})

@app.route('/login', methods=['POST'])
def login():
    global trading_client, session_token, keep_alive_thread, keep_alive_active

    data = request.json or {}
    username = data.get('username')
    password = data.get('password')
    app_key  = data.get('app_key')

    logger.info(f"Login request data: {data}")
    if not all([username, password, app_key]):
        logger.warning("Missing credentials")
        return jsonify({'success': False, 'error': 'Missing credentials'}), 400

    try:
        trading_client = betfairlightweight.APIClient(
            username=username,
            password=password,
            app_key=app_key
        )

        # Perform interactive login
        trading_client.login_interactive()
        logger.info("Interactive login invoked")

        # Retrieve the token directly from the client
        token = getattr(trading_client, 'session_token', None)

        if not token:
            logger.error("No trading_client.session_token after login_interactive()")
            return jsonify({'success': False, 'error': 'No token returned'}), 401

        # Store and return
        session_token = token
        start_keep_alive()
        logger.info("Login successful, token stored")
        return jsonify({
            'success': True,
            'session_token': session_token,
            'message': 'Login successful'
        })

    except Exception as e:
        logger.exception("Exception during login")
        return jsonify({'success': False, 'error': str(e)}), 500

@app.route('/logout', methods=['POST'])
def logout():
    global trading_client, session_token, keep_alive_active
    try:
        keep_alive_active = False
        if trading_client:
            trading_client.logout()
            logger.info("Logout successful")
        trading_client = None
        session_token = None
        return jsonify({'success': True, 'message': 'Logged out successfully'})
    except Exception as e:
        logger.error(f"Logout error: {e}")
        return jsonify({'success': False, 'error': str(e)}), 500

@app.route('/account', methods=['GET'])
def get_account_info():
    if not trading_client:
        return jsonify({'success': False, 'error': 'Not logged in'}), 401
    try:
        details = trading_client.account.get_account_details()
        funds   = trading_client.account.get_account_funds()
        return jsonify({
            'success': True,
            'account': {
                'currency':           details.currency_code,
                'firstname':          details.first_name,
                'lastname':           details.last_name,
                'available_balance':  funds.available_to_bet_balance,
                'exposure':           funds.exposure,
                'retained_commission': funds.retained_commission
            }
        })
    except Exception as e:
        logger.error(f"Account info error: {e}")
        return jsonify({'success': False, 'error': str(e)}), 500

@app.route('/markets', methods=['GET'])
def get_markets():
    if not trading_client:
        return jsonify({'success': False, 'error': 'Not logged in'}), 401
    try:
        event_types = trading_client.betting.list_event_types()
        markets = [{
            'id': et.event_type.id,
            'name': et.event_type.name,
            'market_count': et.market_count
        } for et in event_types[:10]]
        return jsonify({'success': True, 'markets': markets})
    except Exception as e:
        logger.error(f"Markets error: {e}")
        return jsonify({'success': False, 'error': str(e)}), 500

@app.route('/status', methods=['GET'])
def status():
    return jsonify({
        'logged_in':        trading_client is not None,
        'session_token':    session_token[:10] + '...' if session_token else None,
        'keep_alive_active': keep_alive_active
    })

def keep_alive_worker():
    global keep_alive_active
    while keep_alive_active:
        try:
            trading_client.keep_alive()
            logger.info("Keep-alive OK")
            time.sleep(900)
        except Exception as e:
            logger.error(f"Keep-alive error: {e}")
            time.sleep(60)

def start_keep_alive():
    global keep_alive_thread, keep_alive_active
    keep_alive_active = True
    keep_alive_thread = threading.Thread(target=keep_alive_worker, daemon=True)
    keep_alive_thread.start()
    logger.info("Keep-alive thread started")

if __name__ == '__main__':
    logger.info("Starting BETTA Betfair Service...")
    app.run(host='127.0.0.1', port=5000, debug=True)

