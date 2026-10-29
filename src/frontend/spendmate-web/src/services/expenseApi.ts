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

const FAMILY_ID = "22222222-2222-2222-2222-222222222222";

export const DEVELOPMENT_USER_ID = "11111111-1111-1111-1111-111111111111";

const EXPENSES_URL = `/api/families/${FAMILY_ID}/expenses`;

async function readErrorMessage(response: Response): Promise<string> {
  try {
    const body: unknown = await response.json();

    if (typeof body === "object" && body !== null) {
      const problem = body as Record<string, unknown>;

      if (typeof problem.detail === "string") return problem.detail;
      if (typeof problem.error === "string") return problem.error;
      if (typeof problem.title === "string") return problem.title;
    }
  } catch {
    // The response may not contain a JSON body.
  }

  return `Request failed (${response.status} ${response.statusText})`;
}

async function ensureSuccess(response: Response): Promise<Response> {
  if (!response.ok) {
    throw new Error(await readErrorMessage(response));
  }

  return response;
}

export async function getExpenses(): Promise<ExpenseResponse[]> {
  const response = await ensureSuccess(await fetch(EXPENSES_URL));

  return response.json() as Promise<ExpenseResponse[]>;
}

export async function createExpense(
  request: CreateExpenseRequest,
): Promise<ExpenseResponse> {
  const response = await ensureSuccess(
    await fetch(EXPENSES_URL, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(request),
    }),
  );

  return response.json() as Promise<ExpenseResponse>;
}
