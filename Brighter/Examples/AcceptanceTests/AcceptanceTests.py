import requests
import json

def test_we_can_get_all_tasks():
    #payload = {'taskName': 'test1', 'taskDescription': 'Attempting to test an api from python', 'dueDate':'2012-04-23T18:25:43.511Z'}
    headers = {'content-type' : 'application/json'}
    #requests.post('http://localhost:49743/tasks', data=json.dumps(payload), headers=headers)
    r = requests.get('http://localhost:49743/tasks', headers=headers)
    print (r.statuscode)
    print(r.json)
