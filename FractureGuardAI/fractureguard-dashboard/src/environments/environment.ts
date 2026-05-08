export const environment = {
  production: false,
  apiUrl: 'http://localhost:5000',
  notifierUrl: 'http://localhost:3001',
  msalConfig: {
    auth: {
      clientId: 'dev-client-id',
      authority: 'https://login.microsoftonline.com/dev-tenant',
      redirectUri: 'http://localhost:4200',
    }
  },
  devMode: true,
};
