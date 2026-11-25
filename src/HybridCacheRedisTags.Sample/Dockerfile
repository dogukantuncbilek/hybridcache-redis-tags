FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY nuget.config ./
COPY src/HybridCacheRedisTags.Sample/HybridCacheRedisTags.Sample.csproj src/HybridCacheRedisTags.Sample/
RUN dotnet restore src/HybridCacheRedisTags.Sample/HybridCacheRedisTags.Sample.csproj
COPY src/HybridCacheRedisTags.Sample/ src/HybridCacheRedisTags.Sample/
RUN dotnet publish src/HybridCacheRedisTags.Sample/HybridCacheRedisTags.Sample.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "HybridCacheRedisTags.Sample.dll"]
