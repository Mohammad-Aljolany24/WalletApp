export type UserRole = "User" | "Admin";

export interface User {
  id: string;
  email: string;
  role: UserRole;
  isVerified: boolean;
  isFrozen: boolean;
}
export interface AdminUser {
  id: string;
  email: string;
  role: UserRole;
  isVerified: boolean;
  isFrozen: boolean;
  createdAt: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
}

export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
}

export interface RegisterResponse {
  id: string;
  email: string;
}

export interface PagedResponse<T> {
  items: T[];
  nextCursor: string | null;
}