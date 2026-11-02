import {
  DEVELOPMENT_USER_ID,
  type ExpenseResponse,
} from "../services/expenseApi";
import type { Expense } from "../types/expense";

export function mapExpense(response: ExpenseResponse): Expense {
  return {
    id: response.id,
    description: response.description ?? "",
    // Keep compatibility with the existing API data.
    category:
      response.category === "Food" ? "Food & Dining" : response.category,
    amount: response.amount,
    date: response.expenseDate,
    createdBy:
      response.createdByUserId === DEVELOPMENT_USER_ID
        ? "You"
        : "Family member",
    createdAtUtc: response.createdAtUtc,
  };
}
