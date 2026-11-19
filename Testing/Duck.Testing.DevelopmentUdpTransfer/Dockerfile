FROM mcr.microsoft.com/dotnet/runtime@sha256:0cf09aaccf6a9f68900aed4c73fb28fe25bb59ec85de4dea08ba02240a8635f3
WORKDIR /app
COPY Duck.Testing.DevelopmentUdpTransfer.dll Duck.Testing.DevelopmentUdpTransfer.deps.json Duck.Testing.DevelopmentUdpTransfer.runtimeconfig.json ./
ENTRYPOINT ["dotnet", "Duck.Testing.DevelopmentUdpTransfer.dll"]
