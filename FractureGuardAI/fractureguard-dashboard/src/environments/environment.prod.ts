export const environment = {
  production: true,
  apiUrl: 'http://localhost:5000',
  notifierUrl: 'http://localhost:3001',
  msalConfig: {
    auth: {
      clientId: '__MSAL_CLIENT_ID__',
      authority: '__MSAL_AUTHORITY__',
      redirectUri: 'http://localhost:4200',
    }
  },
  devMode: true,
};
