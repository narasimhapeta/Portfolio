FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["AutoInsurance.Shared/AutoInsurance.Shared.csproj", "AutoInsurance.Shared/"]
COPY ["AutoInsurance.Domain/AutoInsurance.Domain.csproj", "AutoInsurance.Domain/"]
COPY ["AutoInsurance.Migrations/AutoInsurance.Migrations.csproj", "AutoInsurance.Migrations/"]
COPY ["AutoInsurance.DocumentGeneration/AutoInsurance.DocumentGeneration.csproj", "AutoInsurance.DocumentGeneration/"]
RUN dotnet restore "AutoInsurance.DocumentGeneration/AutoInsurance.DocumentGeneration.csproj"
COPY . .
RUN dotnet publish "AutoInsurance.DocumentGeneration/AutoInsurance.DocumentGeneration.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5004
ENV ASPNETCORE_URLS=http://+:5004
ENTRYPOINT ["dotnet", "AutoInsurance.DocumentGeneration.dll"]
