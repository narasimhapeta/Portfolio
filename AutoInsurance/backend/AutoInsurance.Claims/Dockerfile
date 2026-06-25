FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["AutoInsurance.Shared/AutoInsurance.Shared.csproj", "AutoInsurance.Shared/"]
COPY ["AutoInsurance.Domain/AutoInsurance.Domain.csproj", "AutoInsurance.Domain/"]
COPY ["AutoInsurance.Migrations/AutoInsurance.Migrations.csproj", "AutoInsurance.Migrations/"]
COPY ["AutoInsurance.Claims/AutoInsurance.Claims.csproj", "AutoInsurance.Claims/"]
RUN dotnet restore "AutoInsurance.Claims/AutoInsurance.Claims.csproj"
COPY . .
RUN dotnet publish "AutoInsurance.Claims/AutoInsurance.Claims.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5005
ENV ASPNETCORE_URLS=http://+:5005
ENTRYPOINT ["dotnet", "AutoInsurance.Claims.dll"]
