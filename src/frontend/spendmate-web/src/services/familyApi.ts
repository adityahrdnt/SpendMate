import { getJson, postJson, putJson } from "./apiClient";
import type {
  CreateFamilyRequest,
  Family,
  SetActiveFamilyRequest,
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
