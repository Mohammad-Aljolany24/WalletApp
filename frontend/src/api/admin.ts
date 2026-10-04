import { api } from "./client";
import type { AdminUser } from "../types/api";

export const adminApi = {
  getUsers: () => api.get<AdminUser[]>("/admin/users"),

  verifyUser: (id: string) =>
    api.post<{ id: string; isVerified: boolean }>(
      `/admin/users/${id}/verify`
    ),

  freezeUser: (id: string) =>
    api.post<{ id: string; isFrozen: boolean }>(
      `/admin/users/${id}/freeze`
    ),
};