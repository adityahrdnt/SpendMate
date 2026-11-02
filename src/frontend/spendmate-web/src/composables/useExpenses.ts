import { ref } from "vue";
import {
  createExpense as createExpenseApi,
  deleteExpense as deleteExpenseApi,
  getExpenses,
  updateExpense as updateExpenseApi,
  type CreateExpenseRequest,
} from "../services/expenseApi";
import type { Expense } from "../types/expense";
import { mapExpense } from "../utils/expenseMapper";

export function useExpenses() {
  const expenses = ref<Expense[]>([]);

  const isLoading = ref(true);
  const isSaving = ref(false);
  const errorMessage = ref("");
  const saveErrorMessage = ref("");

  const deletingExpenseId = ref<string | null>(null);
  const deleteErrorMessage = ref("");

  async function loadExpenses() {
    isLoading.value = true;
    errorMessage.value = "";

    try {
      const response = await getExpenses();
      expenses.value = response.map(mapExpense);
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
    isSaving.value = true;
    saveErrorMessage.value = "";

    try {
      const response = expenseId
        ? await updateExpenseApi(expenseId, request)
        : await createExpenseApi(request);

      const savedExpense = mapExpense(response);

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
    deletingExpenseId.value = expenseId;
    deleteErrorMessage.value = "";

    try {
      await deleteExpenseApi(expenseId);

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
