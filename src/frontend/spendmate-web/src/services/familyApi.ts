import {
  apiRequest,
  deleteRequest,
  getJson,
  postJson,
  putJson,
} from "./apiClient";

import type {
  CreateFamilyRequest,
  Family,
  FamilyMember,
  SetActiveFamilyRequest,
  UpdateFamilyMemberRoleRequest,
} from "../types/family";

export function getFamilies(): Promise<Family[]> {
  return getJson<Family[]>("/api/families/");
}

export function setActiveFamily(
  request: SetActiveFamilyRequest,
): Promise<void> {
  return putJson<SetActiveFamilyRequest, void>(
    "/api/me/active-family",
    request,
  );
}

export function createFamily(request: CreateFamilyRequest): Promise<Family> {
  return postJson<CreateFamilyRequest, Family>("/api/families/", request);
}

export function getFamilyMembers(familyId: string): Promise<FamilyMember[]> {
  return getJson<FamilyMember[]>(`/api/families/${familyId}/members`);
}

export function updateFamilyMemberRole(
  familyId: string,
  userId: string,
  request: UpdateFamilyMemberRoleRequest,
): Promise<void> {
  return apiRequest<void>(`/api/families/${familyId}/members/${userId}/role`, {
    method: "PATCH",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(request),
  });
}

export function removeFamilyMember(
  familyId: string,
  userId: string,
): Promise<void> {
  return deleteRequest(`/api/families/${familyId}/members/${userId}`);
}
