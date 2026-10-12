import { api } from "./client";
import type {
  LoginRequest,
  LoginResponse,
  RegisterRequest,
  RegisterResponse,
  User,
} from "../types/api";

const BASE = "/api/v1/auth";

export const authApi = {
  register: (data: RegisterRequest) =>
    api.post<RegisterResponse>(`${BASE}/register`, data),

  login: (data: LoginRequest) =>
    api.post<LoginResponse>(`${BASE}/login`, data),

  me: () => api.get<User>(`${BASE}/me`),
};