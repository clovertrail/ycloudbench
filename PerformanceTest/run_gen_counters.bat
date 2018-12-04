@echo off
IF EXIST counters.txt DEL /F counters.txt

dotnet run -u http://signalr3.southeastasia.cloudapp.azure.com:5050 -s 100 -t 1000 -d 1000 -a 100 --UseCounter 1
