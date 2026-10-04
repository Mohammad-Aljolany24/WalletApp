import { api } from "./client";
import type {
  LoginRequest,
  LoginResponse,
  RegisterRequest,
  RegisterResponse,
  User,
} from "../types/api";

export const authApi = {
  register: (data: RegisterRequest) =>
    api.post<RegisterResponse>("/auth/register", data),

  login: (data: LoginRequest) =>
    api.post<LoginResponse>("/auth/login", data),

  me: () => api.get<User>("/auth/me"),
};