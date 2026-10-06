import { api } from "./client";
import type { AdminUser, PagedResponse } from "../types/api";

export const adminApi = {
  getUsers: (cursor?: string | null, limit = 20) => {
    const params = new URLSearchParams();
    if (cursor) params.set("cursor", cursor);
    params.set("limit", limit.toString());
    return api.get<PagedResponse<AdminUser>>(
      `/admin/users?${params.toString()}`
    );
  },

  verifyUser: (id: string) =>
    api.post<{ id: string; isVerified: boolean }>(
      `/admin/users/${id}/verify`
    ),

  freezeUser: (id: string) =>
    api.post<{ id: string; isFrozen: boolean }>(
      `/admin/users/${id}/freeze`
    ),
};