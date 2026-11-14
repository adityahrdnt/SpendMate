export type FamilyType = "Personal" | "Shared";

export type FamilyRole = "Owner" | "Member";

export interface Family {
  id: string;
  name: string;
  type: FamilyType;
  role: FamilyRole;
  joinedAtUtc: string;
}

export interface SetActiveFamilyRequest {
  familyId: string;
}

export interface CreateFamilyRequest {
  name: string;
}
