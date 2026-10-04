import { api } from "./client";

export interface BalanceResponse {
  balance: number;
}

export interface Transaction {
  type: "Deposited" | "Withdrawn";
  amount: number;
  occurredAt: string;
}

export const walletApi = {
  getBalance: () => api.get<BalanceResponse>("/wallet/balance"),

  deposit: (amount: number) =>
    api.post<BalanceResponse>(`/wallet/deposit?amount=${amount}`),

  withdraw: (amount: number) =>
    api.post<BalanceResponse>(`/wallet/withdraw?amount=${amount}`),

  getTransactions: () => api.get<Transaction[]>("/wallet/transactions"),
};