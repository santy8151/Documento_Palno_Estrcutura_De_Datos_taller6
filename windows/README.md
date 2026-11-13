# Windows

1. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download) and Git.
2. Clone the repo and open PowerShell or CMD in this folder.

```powershell
.\run.ps1                 # Spanish map, start D
.\run.ps1 -English        # English map  -> http://localhost:5000/en/
.\run.ps1 -Start A -Port 5001
run.bat D --en            # same, from CMD
```

If PowerShell blocks the script: `Set-ExecutionPolicy -Scope Process Bypass`.
Console only (no browser): `cd ..\DeliveryRoutes; dotnet run -- city.txt packages.txt D --plain`
