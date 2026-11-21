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

export type FamilyInvitationType = "Email" | "Phone";

export interface CreateFamilyInvitationRequest {
  invitationType: FamilyInvitationType;
  email?: string;
  phoneNumber?: string;
}

export interface FamilyInvitation {
  id: string;
  familyId: string;
  invitationType: FamilyInvitationType;
  email: string | null;
  phoneNumber: string | null;
  status: string;
  createdAtUtc: string;
  expiresAtUtc: string;
}

export interface CreateFamilyInvitationResult {
  invitation: FamilyInvitation;
  invitationLink: string;
  wasResent: boolean;
}

export interface AddFamilyMemberByPhoneRequest {
  phoneNumber: string;
}
