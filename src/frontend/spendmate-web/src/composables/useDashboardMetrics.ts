import { computed, type ComputedRef } from "vue";
import type { Expense } from "../types/expense";

export type ExpenseCategory = {
  name: string;
  color: string;
  icon: string;
};

export function useDashboardMetrics(
  dateFilteredExpenses: ComputedRef<Expense[]>,
  categories: ExpenseCategory[],
) {
  const totalSpent = computed(() =>
    dateFilteredExpenses.value.reduce(
      (sum, expense) => sum + expense.amount,
      0,
    ),
  );

  const transactionCount = computed(() => dateFilteredExpenses.value.length);

  const averageExpense = computed(() =>
    transactionCount.value
      ? Math.round(totalSpent.value / transactionCount.value)
      : 0,
  );

  const expenseCategories = computed(() => {
    const knownNames = new Set(categories.map((category) => category.name));

    const additionalCategories = [
      ...new Set(
        dateFilteredExpenses.value
          .map((expense) => expense.category)
          .filter((category) => !knownNames.has(category)),
      ),
    ].map((name) => ({
      name,
      color: "#9ca3af",
      icon: "📦",
    }));

    return [...categories, ...additionalCategories];
  });

  const categoryTotals = computed(() =>
    expenseCategories.value
      .map((category) => ({
        ...category,
        amount: dateFilteredExpenses.value
          .filter((expense) => expense.category === category.name)
          .reduce((sum, expense) => sum + expense.amount, 0),
      }))
      .filter((category) => category.amount > 0)
      .sort((a, b) => b.amount - a.amount),
  );

  const largestCategory = computed(() => categoryTotals.value[0]?.name ?? "—");

  return {
    totalSpent,
    transactionCount,
    averageExpense,
    expenseCategories,
    categoryTotals,
    largestCategory,
  };
}
