# Linux

```bash
# Ubuntu / Debian
sudo apt update && sudo apt install -y dotnet-sdk-8.0 git
# Fedora
sudo dnf install -y dotnet-sdk-8.0 git

cd linux
./run.sh              # Spanish map, start D -> http://localhost:5000/
./run.sh D --en       # English map          -> http://localhost:5000/en/
```

The browser is opened with `xdg-open`; if it is not installed, open the URL by hand.
Console only: `cd ../DeliveryRoutes && dotnet run -- city.txt packages.txt D --plain`
