import { api } from "./client";
import type { PagedResponse } from "../types/api";

const BASE = "/api/v1/wallet";

export interface BalanceResponse {
  balance: number;
}

export interface Transaction {
  type: "Deposited" | "Withdrawn";
  amount: number;
  occurredAt: string;
}

export const walletApi = {
  getBalance: () => api.get<BalanceResponse>(`${BASE}/balance`),

  deposit: (amount: number, idempotencyKey?: string) =>
    api.post<BalanceResponse>(
      `${BASE}/deposit?amount=${amount}`,
      undefined,
      idempotencyKey
    ),

  withdraw: (amount: number, idempotencyKey?: string) =>
    api.post<BalanceResponse>(
      `${BASE}/withdraw?amount=${amount}`,
      undefined,
      idempotencyKey
    ),

  getTransactions: (cursor?: string | null, limit = 20) => {
    const params = new URLSearchParams();
    if (cursor) params.set("cursor", cursor);
    params.set("limit", limit.toString());
    return api.get<PagedResponse<Transaction>>(
      `${BASE}/transactions?${params.toString()}`
    );
  },
};