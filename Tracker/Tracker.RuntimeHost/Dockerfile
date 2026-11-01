# .NET 10の共有実行環境を使用する。
FROM mcr.microsoft.com/dotnet/runtime@sha256:0cf09aaccf6a9f68900aed4c73fb28fe25bb59ec85de4dea08ba02240a8635f3
WORKDIR /app
COPY . /app
ENTRYPOINT ["dotnet", "Tracker.RuntimeHost.dll"]
