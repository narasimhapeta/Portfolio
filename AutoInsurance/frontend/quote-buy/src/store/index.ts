import { combineReducers, configureStore } from '@reduxjs/toolkit';
import { persistReducer, persistStore } from 'redux-persist';
import { quoteApi } from '../api/quoteApi';
import { paymentApi } from '../api/paymentApi';
import quoteReducer from './quoteSlice';
import { encryptedStorage, STORAGE_KEY } from './encryptedStorage';

const persistConfig = {
  key: STORAGE_KEY,
  storage: encryptedStorage,
  whitelist: ['quote'],
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
