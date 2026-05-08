export const environment = {
  production: true,
  apiUrl: 'https://api.fractureguard.example.com',
  notifierUrl: 'https://notifier.fractureguard.example.com',
  msalConfig: {
    auth: {
      clientId: '__MSAL_CLIENT_ID__',
      authority: '__MSAL_AUTHORITY__',
      redirectUri: 'https://fractureguard.example.com',
    }
  },
  devMode: false,
};
