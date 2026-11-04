import type { ExpenseResponse } from "../services/expenseApi";
import type { Expense } from "../types/expense";

export function mapExpense(
  response: ExpenseResponse,
  currentUserId: string,
): Expense {
  return {
    id: response.id,
    description: response.description ?? "",
    category:
      response.category === "Food" ? "Food & Dining" : response.category,
    amount: response.amount,
    date: response.expenseDate,
    createdBy:
      response.createdByUserId === currentUserId ? "You" : "Family member",
    createdAtUtc: response.createdAtUtc,
  };
}
