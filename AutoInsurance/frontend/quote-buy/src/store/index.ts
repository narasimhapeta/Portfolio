import { combineReducers, configureStore } from '@reduxjs/toolkit';
import { createTransform, persistReducer, persistStore } from 'redux-persist';
import { quoteApi } from '../api/quoteApi';
import { paymentApi } from '../api/paymentApi';
import quoteReducer from './quoteSlice';
import type { QuoteState } from './quoteSlice';

const safeQuoteTransform = createTransform<QuoteState, Pick<QuoteState, 'session' | 'policy' | 'currentStep'>>(
  ({ session, policy, currentStep }) => ({ session, policy, currentStep }),
  (state) => state as QuoteState,
  { whitelist: ['quote'] },
);

const persistConfig = {
  key: 'qb_state',
  storage: {
    getItem: (key: string) => localStorage.getItem(key),
    setItem: (key: string, value: string) => { localStorage.setItem(key, value); },
    removeItem: (key: string) => { localStorage.removeItem(key); },
  },
  whitelist: ['quote'],
  transforms: [safeQuoteTransform],
};

const rootReducer = combineReducers({
  quote: quoteReducer,
  [quoteApi.reducerPath]: quoteApi.reducer,
  [paymentApi.reducerPath]: paymentApi.reducer,
});

const persistedReducer = persistReducer(persistConfig, rootReducer);

export const store = configureStore({
  reducer: persistedReducer,
  middleware: (getDefault) =>
    getDefault({ serializableCheck: { ignoredActions: ['persist/PERSIST', 'persist/REHYDRATE'] } })
      .concat(quoteApi.middleware, paymentApi.middleware),
});

export const persistor = persistStore(store);

export type RootState = ReturnType<typeof store.getState>;
export type AppDispatch = typeof store.dispatch;
