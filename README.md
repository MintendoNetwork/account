> [!IMPORTANT]
> This is a WORK IN PROGRESS server and is NOT in a production ready state as of right now

# Account Server
This is a WIP Nintendo Account System implementation in C# for Wii U and 3DS (3DS is not implemented yet :P) 

*Also, please note that I'm a amateur dev at C#, so don't hesitate to fix any stuff that is broken or give suggestions to improve this code!!! :3

# Setup

You will need a postgres server and you would add it to appsettings.json under "DefaultConnection" with the postgres server info.
From there you just add your Mii CDN server into the enviroment file as `CDN_BASE_URL` like this:
``` 
CDN_BASE_URL="https://myexample.com"
```
Then, run it using `dotnet run`.
