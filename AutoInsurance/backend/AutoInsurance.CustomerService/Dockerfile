FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["AutoInsurance.Shared/AutoInsurance.Shared.csproj", "AutoInsurance.Shared/"]
COPY ["AutoInsurance.Domain/AutoInsurance.Domain.csproj", "AutoInsurance.Domain/"]
COPY ["AutoInsurance.Migrations/AutoInsurance.Migrations.csproj", "AutoInsurance.Migrations/"]
COPY ["AutoInsurance.CustomerService/AutoInsurance.CustomerService.csproj", "AutoInsurance.CustomerService/"]
RUN dotnet restore "AutoInsurance.CustomerService/AutoInsurance.CustomerService.csproj"
COPY . .
RUN dotnet publish "AutoInsurance.CustomerService/AutoInsurance.CustomerService.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5002
ENV ASPNETCORE_URLS=http://+:5002
ENTRYPOINT ["dotnet", "AutoInsurance.CustomerService.dll"]
