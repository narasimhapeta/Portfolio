FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["AutoInsurance.Shared/AutoInsurance.Shared.csproj", "AutoInsurance.Shared/"]
COPY ["AutoInsurance.Domain/AutoInsurance.Domain.csproj", "AutoInsurance.Domain/"]
COPY ["AutoInsurance.Migrations/AutoInsurance.Migrations.csproj", "AutoInsurance.Migrations/"]
COPY ["AutoInsurance.QuoteBuy/AutoInsurance.QuoteBuy.csproj", "AutoInsurance.QuoteBuy/"]
RUN dotnet restore "AutoInsurance.QuoteBuy/AutoInsurance.QuoteBuy.csproj"
COPY . .
RUN dotnet publish "AutoInsurance.QuoteBuy/AutoInsurance.QuoteBuy.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5001
ENV ASPNETCORE_URLS=http://+:5001
ENTRYPOINT ["dotnet", "AutoInsurance.QuoteBuy.dll"]
