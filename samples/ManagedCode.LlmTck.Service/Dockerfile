FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet publish samples/ManagedCode.LlmTck.Service/ManagedCode.LlmTck.Service.csproj \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

LABEL org.opencontainers.image.source="https://github.com/managedcode/LlmTck" \
      org.opencontainers.image.description="ManagedCode LLM TCK HTTP service" \
      org.opencontainers.image.licenses="MIT"

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ManagedCode.LlmTck.Service.dll"]
