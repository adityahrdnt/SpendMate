export interface AuthResponse {
  userId: string;
  email: string;
  displayName: string;
  familyId: string;
  familyName: string;
}

export interface RegisterRequest {
  email: string;
  phoneNumber: string;
  displayName: string;
  password: string;
  familyName: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}
