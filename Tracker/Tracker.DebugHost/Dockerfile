# ASP.NET Core 10の共有実行環境を使用する。
FROM mcr.microsoft.com/dotnet/aspnet@sha256:8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca
WORKDIR /app
COPY . /app
ENTRYPOINT ["dotnet", "Tracker.DebugHost.dll"]
