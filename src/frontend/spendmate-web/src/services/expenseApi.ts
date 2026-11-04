import { deleteRequest, getJson, postJson, putJson } from "./apiClient";

export interface ExpenseResponse {
  id: string;
  familyId: string;
  createdByUserId: string;
  amount: number;
  category: string;
  description: string | null;
  expenseDate: string;
  createdAtUtc: string;
}

export interface CreateExpenseRequest {
  amount: number;
  category: string;
  description: string | null;
  expenseDate: string;
}

export interface UpdateExpenseRequest {
  amount: number;
  category: string;
  description: string | null;
  expenseDate: string;
}

function getExpensesUrl(familyId: string): string {
  if (!familyId.trim()) {
    throw new Error("A valid family ID is required.");
  }

  return `/api/families/${encodeURIComponent(familyId)}/expenses`;
}

export function getExpenses(familyId: string): Promise<ExpenseResponse[]> {
  return getJson<ExpenseResponse[]>(getExpensesUrl(familyId));
}

export function createExpense(
  familyId: string,
  request: CreateExpenseRequest,
): Promise<ExpenseResponse> {
  return postJson<CreateExpenseRequest, ExpenseResponse>(
    getExpensesUrl(familyId),
    request,
  );
}

export function updateExpense(
  familyId: string,
  expenseId: string,
  request: UpdateExpenseRequest,
): Promise<ExpenseResponse> {
  return putJson<UpdateExpenseRequest, ExpenseResponse>(
    `${getExpensesUrl(familyId)}/${encodeURIComponent(expenseId)}`,
    request,
  );
}

export function deleteExpense(
  familyId: string,
  expenseId: string,
): Promise<void> {
  return deleteRequest(
    `${getExpensesUrl(familyId)}/${encodeURIComponent(expenseId)}`,
  );
}
