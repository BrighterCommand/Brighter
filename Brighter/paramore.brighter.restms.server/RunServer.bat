REM You need to run at an elevated command prompt if you need to reserve the url you are using
REM netsh http add urlacl url=http://+:8080/ user=machine\username
.\bin\Debug\paramore.brighter.restms.server.exe
REM netsh http delete urlacl url=http://+:8080/

