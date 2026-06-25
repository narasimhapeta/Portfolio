import { configureStore } from '@reduxjs/toolkit';
import { customerApi } from '../api/customerApi';
import { claimsApi } from '../api/claimsApi';
import { paymentApi } from '../api/paymentApi';

export const store = configureStore({
  reducer: {
    [customerApi.reducerPath]: customerApi.reducer,
    [claimsApi.reducerPath]: claimsApi.reducer,
    [paymentApi.reducerPath]: paymentApi.reducer,
  },
  middleware: (getDefault) =>
    getDefault().concat(customerApi.middleware, claimsApi.middleware, paymentApi.middleware),
});

export type RootState = ReturnType<typeof store.getState>;
export type AppDispatch = typeof store.dispatch;
