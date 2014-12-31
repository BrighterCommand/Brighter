REM You need to run at an elevated command prompt if you need to reserve the url you are using
REM netsh http add urlacl url=http://+:8080/ user=machine\username
netsh http add urlacl url=http://10.50.4.18:3416/ user=HUDDLE\ian
.\bin\Debug\paramore.brighter.restms.server.exe
REM netsh http delete urlacl url=http://+:8080/
netsh http delete urlaclurl=http://10.50.4.18:3416/

