"""Readiness checks against the running API; never connects to or writes SQL Server."""
import json
import urllib.error
import urllib.request

BASE = 'http://127.0.0.1:5080'

def request(path, payload=None):
    body = None if payload is None else json.dumps(payload).encode()
    req = urllib.request.Request(BASE + path, data=body, headers={'Content-Type': 'application/json'})
    try:
        with urllib.request.urlopen(req) as response:
            return response.status, json.load(response)
    except urllib.error.HTTPError as error:
        return error.code, json.load(error)

status, body = request('/api/health')
assert status == 200 and body == {'name': 'lior_cal', 'status': 'ok'}
credentials = {'server': 'unused.invalid', 'user': 'test', 'password': 'test'}
base = {'connection': credentials, 'database': 'test'}
cases = [
    ('/api/connect', {'server': '', 'user': 'test', 'password': ''}, 'required'),
    ('/api/database', {'connection': None, 'database': ''}, 'database'),
    ('/api/preview', base, 'at least one'),
    ('/api/apply', {**base, 'gain': 9}, 'Boolean'),
    ('/api/preview', {**base, 'amplitude': {'lowIds': '3-1', 'status': 0, 'updateStatus': True, 'updateTime': True}}, 'range'),
    ('/api/preview', {**base, 'amplitude': {'lowIds': '1-10001', 'status': 0, 'updateStatus': True, 'updateTime': True}}, 'too many'),
    ('/api/apply', {**base, 'amplitude': {'lowIds': '1', 'status': 0, 'updateStatus': False, 'updateTime': False}}, 'status or time'),
    ('/api/preview', {**base, 'overall': {'midIds': '1', 'highIds': '1', 'all': False, 'typeB': [], 'status': 2, 'updateStatus': True, 'updateTime': True}}, 'TB'),
    ('/api/apply', {**base, 'overall': {'midIds': '1', 'highIds': '1', 'all': True, 'typeB': [], 'status': 4, 'updateStatus': True, 'updateTime': True}}, 'status'),
]
for path, payload, expected in cases:
    status, body = request(path, payload)
    assert status == 400 and expected.lower() in body['message'].lower(), (path, status, body)
with urllib.request.urlopen('http://127.0.0.1:5173/api/health') as response:
    assert json.load(response)['name'] == 'lior_cal'
with urllib.request.urlopen('http://127.0.0.1:5173/') as response:
    assert '<title>lior_cal</title>' in response.read().decode()
print('12 checks passed: API health, nine invalid-request cases, Vite API proxy, frontend entry.')
