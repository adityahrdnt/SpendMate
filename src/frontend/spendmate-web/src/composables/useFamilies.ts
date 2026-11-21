import { ref } from "vue";
import { ApiError } from "../services/apiClient";
import {
  createFamily as createFamilyApi,
  getFamilies,
  getFamilyMembers,
  removeFamilyMember as removeFamilyMemberApi,
  setActiveFamily as setActiveFamilyApi,
  updateFamilyMemberRole as updateFamilyMemberRoleApi,
  addFamilyMemberByPhone as addFamilyMemberByPhoneApi,
  createFamilyInvitation as createFamilyInvitationApi,
  revokeFamilyInvitation as revokeFamilyInvitationApi,
} from "../services/familyApi";

import type {
  Family,
  FamilyMember,
  UpdateFamilyMemberRoleRequest,
  AddFamilyMemberByPhoneRequest,
  CreateFamilyInvitationRequest,
} from "../types/family";

export function useFamilies() {
  const families = ref<Family[]>([]);
  const isLoadingFamilies = ref(false);
  const isSwitchingFamily = ref(false);
  const isCreatingFamily = ref(false);
  const familyMembers = ref<FamilyMember[]>([]);
  const isLoadingMembers = ref(false);
  const isUpdatingMemberRole = ref(false);
  const isRemovingMember = ref(false);
  const memberErrorMessage = ref("");
  const errorMessage = ref("");
  const isAddingMember = ref(false);
  const isCreatingInvitation = ref(false);
  const isRevokingInvitation = ref(false);
  const invitationErrorMessage = ref("");
  const invitationSuccessMessage = ref("");

  async function loadFamilies(): Promise<boolean> {
    isLoadingFamilies.value = true;
    errorMessage.value = "";

    try {
      families.value = await getFamilies();
      return true;
    } catch (error) {
      errorMessage.value =
        error instanceof ApiError
          ? error.message
          : error instanceof Error
            ? error.message
            : "Unable to load families. Please try again.";

      return false;
    } finally {
      isLoadingFamilies.value = false;
    }
  }

  async function switchActiveFamily(familyId: string): Promise<boolean> {
    errorMessage.value = "";

    if (isSwitchingFamily.value) {
      return false;
    }

    isSwitchingFamily.value = true;

    try {
      await setActiveFamilyApi({ familyId });
      return true;
    } catch (error) {
      errorMessage.value =
        error instanceof ApiError
          ? error.message
          : error instanceof Error
            ? error.message
            : "Unable to switch family. Please try again.";

      return false;
    } finally {
      isSwitchingFamily.value = false;
    }
  }

  async function createFamily(name: string): Promise<boolean> {
    errorMessage.value = "";

    if (isCreatingFamily.value) {
      return false;
    }

    isCreatingFamily.value = true;

    try {
      await createFamilyApi({ name });
      await loadFamilies();
      return true;
    } catch (error) {
      errorMessage.value =
        error instanceof ApiError
          ? error.message
          : error instanceof Error
            ? error.message
            : "Unable to create family. Please try again.";

      return false;
    } finally {
      isCreatingFamily.value = false;
    }
  }

  function clearFamilyMembers(): void {
    familyMembers.value = [];
    memberErrorMessage.value = "";
  }

  async function loadFamilyMembers(familyId: string): Promise<boolean> {
    familyMembers.value = [];
    isLoadingMembers.value = true;
    memberErrorMessage.value = "";

    try {
      familyMembers.value = await getFamilyMembers(familyId);
      return true;
    } catch (error) {
      memberErrorMessage.value =
        error instanceof ApiError
          ? error.message
          : error instanceof Error
            ? error.message
            : "Unable to load family members. Please try again.";

      return false;
    } finally {
      isLoadingMembers.value = false;
    }
  }

  async function updateMemberRole(
    familyId: string,
    userId: string,
    request: UpdateFamilyMemberRoleRequest,
  ): Promise<boolean> {
    memberErrorMessage.value = "";

    if (isUpdatingMemberRole.value || isRemovingMember.value) {
      return false;
    }

    isUpdatingMemberRole.value = true;

    try {
      await updateFamilyMemberRoleApi(familyId, userId, request);
      return true;
    } catch (error) {
      memberErrorMessage.value =
        error instanceof ApiError
          ? error.message
          : error instanceof Error
            ? error.message
            : "Unable to update member role. Please try again.";

      return false;
    } finally {
      isUpdatingMemberRole.value = false;
    }
  }

  async function removeMember(
    familyId: string,
    userId: string,
  ): Promise<boolean> {
    memberErrorMessage.value = "";

    if (isRemovingMember.value || isUpdatingMemberRole.value) {
      return false;
    }

    isRemovingMember.value = true;

    try {
      await removeFamilyMemberApi(familyId, userId);
      return true;
    } catch (error) {
      memberErrorMessage.value =
        error instanceof ApiError
          ? error.message
          : error instanceof Error
            ? error.message
            : "Unable to remove family member. Please try again.";

      return false;
    } finally {
      isRemovingMember.value = false;
    }
  }

  async function addMemberByPhone(
    familyId: string,
    request: AddFamilyMemberByPhoneRequest,
  ): Promise<boolean> {
    memberErrorMessage.value = "";

    if (isAddingMember.value) {
      return false;
    }

    isAddingMember.value = true;

    try {
      await addFamilyMemberByPhoneApi(familyId, request);
      await loadFamilyMembers(familyId);
      return true;
    } catch (error) {
      memberErrorMessage.value =
        error instanceof ApiError
          ? error.message
          : error instanceof Error
            ? error.message
            : "Unable to add family member. Please try again.";

      return false;
    } finally {
      isAddingMember.value = false;
    }
  }

  async function createInvitation(
    familyId: string,
    request: CreateFamilyInvitationRequest,
  ) {
    invitationErrorMessage.value = "";
    invitationSuccessMessage.value = "";

    if (isCreatingInvitation.value) {
      return null;
    }

    isCreatingInvitation.value = true;

    try {
      const result = await createFamilyInvitationApi(familyId, request);

      invitationSuccessMessage.value = result.wasResent
        ? "Invitation resent successfully."
        : "Invitation created successfully.";

      return result;
    } catch (error) {
      invitationErrorMessage.value =
        error instanceof ApiError
          ? error.message
          : error instanceof Error
            ? error.message
            : "Unable to create invitation. Please try again.";

      return null;
    } finally {
      isCreatingInvitation.value = false;
    }
  }

  async function revokeInvitation(
    familyId: string,
    invitationId: string,
  ): Promise<boolean> {
    invitationErrorMessage.value = "";

    if (isRevokingInvitation.value) {
      return false;
    }

    isRevokingInvitation.value = true;

    try {
      await revokeFamilyInvitationApi(familyId, invitationId);
      invitationSuccessMessage.value = "Invitation revoked successfully.";
      return true;
    } catch (error) {
      invitationErrorMessage.value =
        error instanceof ApiError
          ? error.message
          : error instanceof Error
            ? error.message
            : "Unable to revoke invitation. Please try again.";

      return false;
    } finally {
      isRevokingInvitation.value = false;
    }
  }

  return {
    families,
    isLoadingFamilies,
    isSwitchingFamily,
    isCreatingFamily,
    errorMessage,
    loadFamilies,
    switchActiveFamily,
    createFamily,
    familyMembers,
    isLoadingMembers,
    isUpdatingMemberRole,
    isRemovingMember,
    memberErrorMessage,
    loadFamilyMembers,
    clearFamilyMembers,
    updateMemberRole,
    removeMember,
    isAddingMember,
    isCreatingInvitation,
    isRevokingInvitation,
    invitationErrorMessage,
    invitationSuccessMessage,
    addMemberByPhone,
    createInvitation,
    revokeInvitation,
  };
}
