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

export interface FamilyMember {
  userId: string;
  displayName: string;
  email: string;
  role: FamilyRole;
  joinedAtUtc: string;
}

export interface UpdateFamilyMemberRoleRequest {
  role: FamilyRole;
}
