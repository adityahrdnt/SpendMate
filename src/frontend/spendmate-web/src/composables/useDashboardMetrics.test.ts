import { computed, ref } from "vue";
import { describe, expect, it } from "vitest";
import { useDashboardMetrics } from "./useDashboardMetrics";
import type { Expense } from "../types/expense";

const categories = [
  { name: "Groceries", color: "#3c8c70", icon: "🛒" },
  { name: "Food & Dining", color: "#e9a23b", icon: "🍽️" },
  { name: "Transport", color: "#6587d8", icon: "🚗" },
];

const sampleExpenses: Expense[] = [
  {
    id: "1",
    description: "Groceries",
    category: "Groceries",
    amount: 100000,
    date: "2026-10-01",
    createdBy: "You",
    createdAtUtc: "2026-10-01T10:00:00Z",
  },
  {
    id: "2",
    description: "Lunch",
    category: "Food & Dining",
    amount: 50000,
    date: "2026-10-02",
    createdBy: "You",
    createdAtUtc: "2026-10-02T10:00:00Z",
  },
  {
    id: "3",
    description: "Taxi",
    category: "Transport",
    amount: 25000,
    date: "2026-10-03",
    createdBy: "Family member",
    createdAtUtc: "2026-10-03T10:00:00Z",
  },
];

describe("useDashboardMetrics", () => {
  it("calculates total, transaction count, and average expense", () => {
    const expenses = ref(sampleExpenses);

    const metrics = useDashboardMetrics(
      computed(() => expenses.value),
      categories,
    );

    expect(metrics.totalSpent.value).toBe(175000);
    expect(metrics.transactionCount.value).toBe(3);
    expect(metrics.averageExpense.value).toBe(58333);
  });

  it("calculates category totals in descending order", () => {
    const metrics = useDashboardMetrics(
      computed(() => sampleExpenses),
      categories,
    );

    expect(
      metrics.categoryTotals.value.map((category) => ({
        name: category.name,
        amount: category.amount,
      })),
    ).toEqual([
      { name: "Groceries", amount: 100000 },
      { name: "Food & Dining", amount: 50000 },
      { name: "Transport", amount: 25000 },
    ]);

    expect(metrics.largestCategory.value).toBe("Groceries");
  });

  it("includes additional categories not in the predefined list", () => {
    const expenses = [
      ...sampleExpenses,
      {
        ...sampleExpenses[0],
        id: "4",
        category: "Healthcare",
        amount: 200000,
      },
    ];

    const metrics = useDashboardMetrics(
      computed(() => expenses),
      categories,
    );

    expect(
      metrics.expenseCategories.value.map((category) => category.name),
    ).toContain("Healthcare");

    expect(metrics.categoryTotals.value[0].name).toBe("Healthcare");
    expect(metrics.largestCategory.value).toBe("Healthcare");
  });

  it("returns zero metrics when there are no expenses", () => {
    const metrics = useDashboardMetrics(
      computed(() => []),
      categories,
    );

    expect(metrics.totalSpent.value).toBe(0);
    expect(metrics.transactionCount.value).toBe(0);
    expect(metrics.averageExpense.value).toBe(0);
    expect(metrics.categoryTotals.value).toEqual([]);
    expect(metrics.largestCategory.value).toBe("—");
  });

  it("reacts when the expense collection changes", () => {
    const expenses = ref(sampleExpenses);

    const metrics = useDashboardMetrics(
      computed(() => expenses.value),
      categories,
    );

    expenses.value = [sampleExpenses[0]];

    expect(metrics.totalSpent.value).toBe(100000);
    expect(metrics.transactionCount.value).toBe(1);
    expect(metrics.averageExpense.value).toBe(100000);
  });
});
