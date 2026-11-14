import { ref } from "vue";
import { ApiError } from "../services/apiClient";
import {
  createFamily as createFamilyApi,
  getFamilies,
  setActiveFamily as setActiveFamilyApi,
} from "../services/familyApi";
import type { Family } from "../types/family";

export function useFamilies() {
  const families = ref<Family[]>([]);
  const isLoadingFamilies = ref(false);
  const isSwitchingFamily = ref(false);
  const isCreatingFamily = ref(false);
  const errorMessage = ref("");

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

  return {
    families,
    isLoadingFamilies,
    isSwitchingFamily,
    isCreatingFamily,
    errorMessage,
    loadFamilies,
    switchActiveFamily,
    createFamily,
  };
}
