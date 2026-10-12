import { api } from "./client";
import type { AdminUser, PagedResponse } from "../types/api";

const BASE = "/api/v1/admin";

export const adminApi = {
  getUsers: (cursor?: string | null, limit = 20) => {
    const params = new URLSearchParams();
    if (cursor) params.set("cursor", cursor);
    params.set("limit", limit.toString());
    return api.get<PagedResponse<AdminUser>>(
      `${BASE}/users?${params.toString()}`
    );
  },

  verifyUser: (id: string) =>
    api.post<{ id: string; isVerified: boolean }>(
      `${BASE}/users/${id}/verify`
    ),

  freezeUser: (id: string) =>
    api.post<{ id: string; isFrozen: boolean }>(
      `${BASE}/users/${id}/freeze`
    ),
};