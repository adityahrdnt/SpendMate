import { ref, toValue, type MaybeRefOrGetter } from "vue";
import {
  createExpense as createExpenseApi,
  deleteExpense as deleteExpenseApi,
  getExpenses,
  updateExpense as updateExpenseApi,
  type CreateExpenseRequest,
} from "../services/expenseApi";
import type { Expense } from "../types/expense";
import { mapExpense } from "../utils/expenseMapper";

export function useExpenses(
  familyId: MaybeRefOrGetter<string>,
  currentUserId: MaybeRefOrGetter<string>,
) {
  const expenses = ref<Expense[]>([]);

  const isLoading = ref(true);
  const isSaving = ref(false);
  const errorMessage = ref("");
  const saveErrorMessage = ref("");

  const deletingExpenseId = ref<string | null>(null);
  const deleteErrorMessage = ref("");

  async function loadExpenses() {
    const activeFamilyId = toValue(familyId);
    const activeUserId = toValue(currentUserId);

    if (!activeFamilyId || !activeUserId) {
      expenses.value = [];
      isLoading.value = false;
      return;
    }

    isLoading.value = true;
    errorMessage.value = "";

    try {
      const response = await getExpenses(activeFamilyId);
      expenses.value = response.map((item) => mapExpense(item, activeUserId));
    } catch (error) {
      errorMessage.value =
        error instanceof Error
          ? error.message
          : "Unable to load expenses. Please try again.";
    } finally {
      isLoading.value = false;
    }
  }

  async function saveExpense(
    request: CreateExpenseRequest,
    expenseId?: string,
  ): Promise<Expense | null> {
    const activeFamilyId = toValue(familyId);
    const activeUserId = toValue(currentUserId);

    if (!activeFamilyId || !activeUserId) {
      saveErrorMessage.value = "An authenticated family session is required.";
      return null;
    }

    isSaving.value = true;
    saveErrorMessage.value = "";

    try {
      const response = expenseId
        ? await updateExpenseApi(activeFamilyId, expenseId, request)
        : await createExpenseApi(activeFamilyId, request);

      const savedExpense = mapExpense(response, activeUserId);

      expenses.value = [
        savedExpense,
        ...expenses.value.filter((expense) => expense.id !== savedExpense.id),
      ];

      return savedExpense;
    } catch (error) {
      saveErrorMessage.value =
        error instanceof Error
          ? error.message
          : "Unable to save the expense. Please try again.";

      return null;
    } finally {
      isSaving.value = false;
    }
  }

  async function removeExpense(expenseId: string): Promise<boolean> {
    const activeFamilyId = toValue(familyId);

    if (!activeFamilyId) {
      deleteErrorMessage.value = "An authenticated family session is required.";
      return false;
    }

    deletingExpenseId.value = expenseId;
    deleteErrorMessage.value = "";

    try {
      await deleteExpenseApi(activeFamilyId, expenseId);

      expenses.value = expenses.value.filter(
        (expense) => expense.id !== expenseId,
      );

      return true;
    } catch (error) {
      deleteErrorMessage.value =
        error instanceof Error
          ? error.message
          : "Unable to delete the expense. Please try again.";

      return false;
    } finally {
      deletingExpenseId.value = null;
    }
  }

  return {
    expenses,
    isLoading,
    isSaving,
    errorMessage,
    saveErrorMessage,
    deletingExpenseId,
    deleteErrorMessage,
    loadExpenses,
    saveExpense,
    removeExpense,
  };
}
