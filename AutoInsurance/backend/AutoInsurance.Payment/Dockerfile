FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["AutoInsurance.Shared/AutoInsurance.Shared.csproj", "AutoInsurance.Shared/"]
COPY ["AutoInsurance.Domain/AutoInsurance.Domain.csproj", "AutoInsurance.Domain/"]
COPY ["AutoInsurance.Migrations/AutoInsurance.Migrations.csproj", "AutoInsurance.Migrations/"]
COPY ["AutoInsurance.Payment/AutoInsurance.Payment.csproj", "AutoInsurance.Payment/"]
RUN dotnet restore "AutoInsurance.Payment/AutoInsurance.Payment.csproj"
COPY . .
RUN dotnet publish "AutoInsurance.Payment/AutoInsurance.Payment.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5003
ENV ASPNETCORE_URLS=http://+:5003
ENTRYPOINT ["dotnet", "AutoInsurance.Payment.dll"]
