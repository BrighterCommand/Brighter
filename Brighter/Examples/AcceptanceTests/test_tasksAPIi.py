import requests
import json


def test_we_can_get_all_tasks():
    payload = {'taskName': 'test1', 'taskDescription': 'Attempting to test an api from python', 'dueDate': '2012-04-23T18:25:43.511Z'}
    post_headers = {'Content-Type': 'application/json'}
    post = requests.post('http://localhost:49743/tasks', data=json.dumps(payload), headers=post_headers)
    assert post.status_code == 201
    assert post.text == '{"dueDate":"23\\/04\\/2012 19:25:43","taskDescription":"Attempting to test an api from python","taskName":"test1"}'

    get_headers = {'Accept': 'application/json'}
    get = requests.get('http://localhost:49743/tasks', headers=get_headers)
    assert get.status_code == 200
