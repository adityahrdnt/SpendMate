import { mount, flushPromises } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import App from "./App.vue";
import {
  createExpense,
  deleteExpense,
  getExpenses,
  updateExpense,
  DEVELOPMENT_USER_ID,
} from "./services/expenseApi";

vi.mock("./services/expenseApi", () => ({
  getExpenses: vi.fn(),
  createExpense: vi.fn(),
  updateExpense: vi.fn(),
  deleteExpense: vi.fn(),
  DEVELOPMENT_USER_ID: "11111111-1111-1111-1111-111111111111",
}));

const sampleExpense = {
  id: "f8446c14-22d0-4590-9c62-fd02c3008dc6",
  familyId: "22222222-2222-2222-2222-222222222222",
  createdByUserId: DEVELOPMENT_USER_ID,
  amount: 35000,
  category: "Food",
  description: "Lunch",
  expenseDate: "2026-10-28",
  createdAtUtc: "2026-10-28T09:41:35.491812+00:00",
};

describe("SpendMate dashboard", () => {
  beforeEach(() => {
    vi.resetAllMocks();
    vi.mocked(getExpenses).mockResolvedValue([]);
    vi.mocked(deleteExpense).mockResolvedValue(undefined);
  });

  it("displays expenses returned by the API", async () => {
    vi.mocked(getExpenses).mockResolvedValue([sampleExpense]);

    const wrapper = mount(App);
    await flushPromises();

    expect(wrapper.text()).toContain("Lunch");
    expect(wrapper.text()).toContain("Food & Dining");
    expect(wrapper.text()).toContain("You");
    expect(wrapper.text().replace(/\s/g, "")).toContain("Rp35.000");

    wrapper.unmount();
  });

  it("includes unknown API categories in the dashboard breakdown and category filter", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 9, 29, 12));

    try {
      const healthcareExpense = {
        ...sampleExpense,
        id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
        amount: 12500,
        category: "Healthcare",
        description: "Medical checkup",
        expenseDate: "2026-10-28",
      };

      vi.mocked(getExpenses).mockResolvedValue([
        sampleExpense,
        healthcareExpense,
      ]);

      const wrapper = mount(App);
      await flushPromises();

      // Unknown category appears in the dashboard breakdown.
      expect(wrapper.text()).toContain("Healthcare");
      expect(wrapper.text().replace(/\s/g, "")).toContain("Rp12.500");

      // Unknown category is available in the expense filter.
      const categoryFilter = wrapper.get(
        'select[aria-label="Filter by category"]',
      );

      expect(categoryFilter.find('option[value="Healthcare"]').exists()).toBe(
        true,
      );

      // Selecting it filters the transaction list correctly.
      await categoryFilter.setValue("Healthcare");

      expect(wrapper.text()).toContain("Medical checkup");
      expect(wrapper.text()).not.toContain("Lunch");

      wrapper.unmount();
    } finally {
      vi.useRealTimers();
    }
  });

  it("calculates dashboard summary and category breakdown correctly", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 9, 29, 12));

    try {
      const expenses = [
        {
          ...sampleExpense,
          id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
          description: "Weekly groceries",
          category: "Groceries",
          amount: 100000,
          expenseDate: "2026-10-28",
        },
        {
          ...sampleExpense,
          id: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
          description: "Monthly groceries",
          category: "Groceries",
          amount: 50000,
          expenseDate: "2026-10-27",
        },
        {
          ...sampleExpense,
          id: "cccccccc-cccc-cccc-cccc-cccccccccccc",
          description: "Taxi",
          category: "Transport",
          amount: 75000,
          expenseDate: "2026-10-26",
        },
      ];

      vi.mocked(getExpenses).mockResolvedValue(expenses);

      const wrapper = mount(App);
      await flushPromises();

      // Total expenses: 100,000 + 50,000 + 75,000
      expect(wrapper.find(".total-card").text().replace(/\s/g, "")).toContain(
        "Rp225.000",
      );

      // Transaction count: 3
      const summaryCards = wrapper.findAll(".summary-card");

      expect(summaryCards[1].text()).toContain("3");

      // Average expense: 225,000 / 3 = 75,000
      expect(summaryCards[2].text().replace(/\s/g, "")).toContain("Rp75.000");

      // Largest category: Groceries (150,000)
      expect(summaryCards[3].text()).toContain("Groceries");

      // Category breakdown
      const categoryRows = wrapper.findAll(".category-row");

      expect(categoryRows).toHaveLength(2);
      expect(categoryRows[0].text()).toContain("Groceries");
      expect(categoryRows[0].text().replace(/\s/g, "")).toContain("Rp150.000");

      expect(categoryRows[1].text()).toContain("Transport");
      expect(categoryRows[1].text().replace(/\s/g, "")).toContain("Rp75.000");

      wrapper.unmount();
    } finally {
      vi.useRealTimers();
    }
  });

  it("shows an error when loading expenses fails", async () => {
    vi.mocked(getExpenses).mockRejectedValue(new Error("API unavailable"));

    const wrapper = mount(App);
    await flushPromises();

    expect(wrapper.text()).toContain("Unable to load expenses");
    expect(wrapper.text()).toContain("API unavailable");
    expect(wrapper.find('[role="alert"]').exists()).toBe(true);

    // Dashboard content should not render when loading fails.
    expect(wrapper.find(".page-heading").exists()).toBe(false);

    // Retry action should remain available.
    expect(wrapper.text()).toContain("Try again");
    wrapper.unmount();
  });

  it("retries loading expenses and displays data after a previous failure", async () => {
    vi.mocked(getExpenses)
      .mockRejectedValueOnce(new Error("API unavailable"))
      .mockResolvedValueOnce([sampleExpense]);

    const wrapper = mount(App);
    await flushPromises();

    expect(wrapper.find('[role="alert"]').exists()).toBe(true);
    expect(getExpenses).toHaveBeenCalledTimes(1);

    const retryButton = wrapper
      .findAll("button")
      .find((button) => button.text().includes("Try again"));

    expect(retryButton).toBeDefined();

    await retryButton!.trigger("click");
    await flushPromises();

    expect(getExpenses).toHaveBeenCalledTimes(2);
    expect(wrapper.find('[role="alert"]').exists()).toBe(false);
    expect(wrapper.text()).toContain("Lunch");

    wrapper.unmount();
  });

  it("hides dashboard content while expenses are loading", async () => {
    vi.mocked(getExpenses).mockReturnValue(new Promise(() => {}));

    const wrapper = mount(App);

    expect(wrapper.get('[role="status"]').text()).toContain(
      "Loading family expenses...",
    );
    expect(wrapper.find(".page-heading").exists()).toBe(false);

    wrapper.unmount();
  });

  it("creates an expense and updates the dashboard without a page reload", async () => {
    const savedExpense = {
      ...sampleExpense,
      id: "dddddddd-dddd-dddd-dddd-dddddddddddd",
      amount: 5000,
      category: "Transport",
      description: "Grab",
      expenseDate: "2026-10-29",
      createdAtUtc: "2026-10-29T02:02:52.457449+00:00",
    };

    vi.mocked(createExpense).mockResolvedValue(savedExpense);

    const wrapper = mount(App);
    await flushPromises();

    const addButton = wrapper
      .findAll("button")
      .find((button) => button.text().includes("Add expense"));
    expect(addButton).toBeDefined();
    await addButton!.trigger("click");

    await wrapper
      .get('input[placeholder="e.g. Weekly groceries"]')
      .setValue("Grab");
    await wrapper.get('input[placeholder="150000"]').setValue("5000");
    await wrapper.get('[role="dialog"] select').setValue("Transport");
    await wrapper.get('input[type="date"]').setValue("2026-10-29");

    await wrapper.get("form.expense-form").trigger("submit");
    await flushPromises();

    expect(createExpense).toHaveBeenCalledWith({
      amount: 5000,
      category: "Transport",
      description: "Grab",
      expenseDate: "2026-10-29",
    });
    expect(wrapper.text()).toContain("Grab");
    expect(wrapper.text().replace(/\s/g, "")).toContain("Rp5.000");
    expect(wrapper.find('[role="dialog"]').exists()).toBe(false);

    wrapper.unmount();
  });

  it("keeps the modal open and shows an error when creating an expense fails", async () => {
    vi.mocked(createExpense).mockRejectedValue(new Error("Save failed"));

    const wrapper = mount(App);
    await flushPromises();

    const addButton = wrapper
      .findAll("button")
      .find((button) => button.text().includes("Add expense"));
    expect(addButton).toBeDefined();
    await addButton!.trigger("click");

    await wrapper
      .get('input[placeholder="e.g. Weekly groceries"]')
      .setValue("Grab");
    await wrapper.get('input[placeholder="150000"]').setValue("5000");
    await wrapper.get('input[type="date"]').setValue("2026-10-29");

    await wrapper.get("form.expense-form").trigger("submit");
    await flushPromises();

    expect(wrapper.find('[role="dialog"]').exists()).toBe(true);
    expect(wrapper.text()).toContain("Save failed");
    expect(createExpense).toHaveBeenCalledOnce();

    wrapper.unmount();
  });

  it.each([
    ["", "5000", "2026-10-29", "empty description"],
    ["Grab", "0", "2026-10-29", "zero amount"],
    ["Grab", "-5000", "2026-10-29", "negative amount"],
    ["Grab", "5000.50", "2026-10-29", "decimal amount"],
  ])(
    "rejects invalid expense input: %s (%s)",
    async (description, amount, date) => {
      const wrapper = mount(App);
      await flushPromises();

      const addButton = wrapper
        .findAll("button")
        .find((button) => button.text().includes("Add expense"));

      expect(addButton).toBeDefined();
      await addButton!.trigger("click");

      await wrapper
        .get('input[placeholder="e.g. Weekly groceries"]')
        .setValue(description);

      await wrapper.get('input[placeholder="150000"]').setValue(amount);

      await wrapper.get('input[type="date"]').setValue(date);

      await wrapper.get("form.expense-form").trigger("submit");
      await flushPromises();

      expect(createExpense).not.toHaveBeenCalled();
      expect(wrapper.find('[role="dialog"]').exists()).toBe(true);
      expect(wrapper.text()).toContain(
        "Enter a valid description, positive whole amount, and date.",
      );

      wrapper.unmount();
    },
  );

  it("uses the local date as the default date when opening the add expense modal", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 9, 30, 1, 0, 0));

    try {
      const wrapper = mount(App);
      await flushPromises();

      const addButton = wrapper
        .findAll("button")
        .find((button) => button.text().includes("Add expense"));

      expect(addButton).toBeDefined();
      await addButton!.trigger("click");

      expect(
        (wrapper.get('input[type="date"]').element as HTMLInputElement).value,
      ).toBe("2026-10-30");

      wrapper.unmount();
    } finally {
      vi.useRealTimers();
    }
  });

  it("edits an expense and updates the dashboard", async () => {
    vi.mocked(getExpenses).mockResolvedValue([sampleExpense]);

    const updatedExpense = {
      ...sampleExpense,
      amount: 45000,
      category: "Groceries",
      description: "Weekly groceries",
      expenseDate: "2026-10-29",
    };
    vi.mocked(updateExpense).mockResolvedValue(updatedExpense);

    const wrapper = mount(App);
    await flushPromises();

    const row = wrapper
      .findAll("tbody tr")
      .find((item) => item.text().includes("Lunch"));
    expect(row).toBeDefined();

    await row!.get("button").trigger("click");
    expect(wrapper.find('[role="dialog"]').text()).toContain("Edit expense");

    await wrapper
      .get('input[placeholder="e.g. Weekly groceries"]')
      .setValue("Weekly groceries");
    await wrapper.get('input[placeholder="150000"]').setValue("45000");
    await wrapper.get('[role="dialog"] select').setValue("Groceries");
    await wrapper.get('input[type="date"]').setValue("2026-10-29");

    await wrapper.get("form.expense-form").trigger("submit");
    await flushPromises();

    expect(updateExpense).toHaveBeenCalledWith(sampleExpense.id, {
      amount: 45000,
      category: "Groceries",
      description: "Weekly groceries",
      expenseDate: "2026-10-29",
    });
    expect(wrapper.text()).toContain("Weekly groceries");
    expect(wrapper.text().replace(/\s/g, "")).toContain("Rp45.000");
    expect(wrapper.text()).not.toContain("Lunch");
    expect(wrapper.find('[role="dialog"]').exists()).toBe(false);

    wrapper.unmount();
  });

  it("keeps the edit modal open and shows an error when updating fails", async () => {
    vi.mocked(getExpenses).mockResolvedValue([sampleExpense]);
    vi.mocked(updateExpense).mockRejectedValue(new Error("Update failed"));

    const wrapper = mount(App);
    await flushPromises();

    const row = wrapper
      .findAll("tbody tr")
      .find((item) => item.text().includes("Lunch"));
    expect(row).toBeDefined();

    await row!.get("button").trigger("click");
    await wrapper
      .get('input[placeholder="e.g. Weekly groceries"]')
      .setValue("Updated lunch");

    await wrapper.get("form.expense-form").trigger("submit");
    await flushPromises();

    expect(updateExpense).toHaveBeenCalledOnce();
    expect(wrapper.find('[role="dialog"]').exists()).toBe(true);
    expect(wrapper.text()).toContain("Update failed");

    wrapper.unmount();
  });

  it("deletes an expense after confirmation", async () => {
    vi.mocked(getExpenses).mockResolvedValue([sampleExpense]);
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(true);

    const wrapper = mount(App);
    await flushPromises();

    const row = wrapper
      .findAll("tbody tr")
      .find((item) => item.text().includes("Lunch"));
    expect(row).toBeDefined();

    const deleteButton = row!
      .findAll("button")
      .find((button) => button.text().includes("Delete"));
    expect(deleteButton).toBeDefined();

    await deleteButton!.trigger("click");
    await flushPromises();

    expect(confirmSpy).toHaveBeenCalledWith(
      'Delete "Lunch"? This action cannot be undone.',
    );
    expect(deleteExpense).toHaveBeenCalledWith(sampleExpense.id);
    expect(wrapper.text()).not.toContain("Lunch");

    wrapper.unmount();
    confirmSpy.mockRestore();
  });

  it("does not delete an expense when confirmation is cancelled", async () => {
    vi.mocked(getExpenses).mockResolvedValue([sampleExpense]);
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(false);

    const wrapper = mount(App);
    await flushPromises();

    const row = wrapper
      .findAll("tbody tr")
      .find((item) => item.text().includes("Lunch"));
    expect(row).toBeDefined();

    const deleteButton = row!
      .findAll("button")
      .find((button) => button.text().includes("Delete"));
    expect(deleteButton).toBeDefined();

    await deleteButton!.trigger("click");
    await flushPromises();

    expect(deleteExpense).not.toHaveBeenCalled();
    expect(wrapper.text()).toContain("Lunch");

    wrapper.unmount();
    confirmSpy.mockRestore();
  });

  it("shows an error when deleting an expense fails", async () => {
    vi.mocked(getExpenses).mockResolvedValue([sampleExpense]);
    vi.mocked(deleteExpense).mockRejectedValue(new Error("Delete failed"));
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(true);

    const wrapper = mount(App);
    await flushPromises();

    const row = wrapper
      .findAll("tbody tr")
      .find((item) => item.text().includes("Lunch"));
    expect(row).toBeDefined();

    const deleteButton = row!
      .findAll("button")
      .find((button) => button.text().includes("Delete"));
    expect(deleteButton).toBeDefined();

    await deleteButton!.trigger("click");
    await flushPromises();

    expect(deleteExpense).toHaveBeenCalledWith(sampleExpense.id);
    expect(wrapper.text()).toContain("Delete failed");
    expect(wrapper.text()).toContain("Lunch");

    wrapper.unmount();
    confirmSpy.mockRestore();
  });

  it("filters expenses by the selected month", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 9, 29, 12));

    try {
      const lastMonthExpense = {
        ...sampleExpense,
        id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
        description: "September groceries",
        category: "Groceries",
        amount: 100000,
        expenseDate: "2026-09-15",
      };

      const currentMonthExpense = {
        ...sampleExpense,
        id: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
        description: "October lunch",
        amount: 35000,
        expenseDate: "2026-10-28",
      };

      vi.mocked(getExpenses).mockResolvedValue([
        lastMonthExpense,
        currentMonthExpense,
      ]);

      const wrapper = mount(App);
      await flushPromises();

      // Dashboard summary for October.
      let summaryCards = wrapper.findAll(".summary-card");

      expect(summaryCards[0].text().replace(/\s/g, "")).toContain("Rp35.000");
      expect(summaryCards[1].text()).toContain("1");
      expect(summaryCards[2].text().replace(/\s/g, "")).toContain("Rp35.000");
      expect(summaryCards[3].text()).toContain("Food & Dining");

      // Category breakdown for October.
      let categoryRows = wrapper.findAll(".category-row");

      expect(categoryRows).toHaveLength(1);
      expect(categoryRows[0].text()).toContain("Food & Dining");
      expect(categoryRows[0].text().replace(/\s/g, "")).toContain("Rp35.000");

      expect(wrapper.text()).toContain("October lunch");
      expect(wrapper.text()).not.toContain("September groceries");
      expect(wrapper.text().replace(/\s/g, "")).toContain("Rp35.000");

      const lastMonthButton = wrapper
        .findAll("button")
        .find((button) => button.text().trim() === "Last month");

      expect(lastMonthButton).toBeDefined();
      await lastMonthButton!.trigger("click");

      expect(wrapper.text()).toContain("September groceries");
      expect(wrapper.text()).not.toContain("October lunch");
      expect(wrapper.text().replace(/\s/g, "")).toContain("Rp100.000");

      // Dashboard summary for September.
      summaryCards = wrapper.findAll(".summary-card");

      expect(summaryCards[0].text().replace(/\s/g, "")).toContain("Rp100.000");
      expect(summaryCards[1].text()).toContain("1");
      expect(summaryCards[2].text().replace(/\s/g, "")).toContain("Rp100.000");
      expect(summaryCards[3].text()).toContain("Groceries");

      // Category breakdown for September.
      categoryRows = wrapper.findAll(".category-row");

      expect(categoryRows).toHaveLength(1);
      expect(categoryRows[0].text()).toContain("Groceries");
      expect(categoryRows[0].text().replace(/\s/g, "")).toContain("Rp100.000");

      wrapper.unmount();
    } finally {
      vi.useRealTimers();
    }
  });

  it("filters expenses by a custom date range", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 9, 29, 12));

    try {
      const expenses = [
        {
          ...sampleExpense,
          id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
          description: "Before range",
          expenseDate: "2026-10-09",
          amount: 10000,
        },
        {
          ...sampleExpense,
          id: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
          description: "Range start",
          expenseDate: "2026-10-10",
          amount: 20000,
        },
        {
          ...sampleExpense,
          id: "cccccccc-cccc-cccc-cccc-cccccccccccc",
          description: "Range end",
          expenseDate: "2026-10-20",
          amount: 30000,
        },
        {
          ...sampleExpense,
          id: "dddddddd-dddd-dddd-dddd-dddddddddddd",
          description: "After range",
          expenseDate: "2026-10-21",
          amount: 40000,
        },
      ];

      vi.mocked(getExpenses).mockResolvedValue(expenses);

      const wrapper = mount(App);
      await flushPromises();

      const customRangeButton = wrapper
        .findAll("button")
        .find((button) => button.text().trim() === "Custom range");

      expect(customRangeButton).toBeDefined();
      await customRangeButton!.trigger("click");

      await wrapper
        .get('input[aria-label="Start date"]')
        .setValue("2026-10-10");
      await wrapper.get('input[aria-label="End date"]').setValue("2026-10-20");

      expect(wrapper.text()).toContain("Range start");
      expect(wrapper.text()).toContain("Range end");
      expect(wrapper.text()).not.toContain("Before range");
      expect(wrapper.text()).not.toContain("After range");
      expect(wrapper.text().replace(/\s/g, "")).toContain("Rp50.000");

      wrapper.unmount();
    } finally {
      vi.useRealTimers();
    }
  });

  it("combines the date range and category filters correctly", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 9, 29, 12));

    try {
      vi.mocked(getExpenses).mockResolvedValue([
        {
          ...sampleExpense,
          id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
          description: "October food",
          category: "Food & Dining",
          amount: 35000,
          expenseDate: "2026-10-15",
        },
        {
          ...sampleExpense,
          id: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
          description: "October transport",
          category: "Transportation",
          amount: 15000,
          expenseDate: "2026-10-16",
        },
        {
          ...sampleExpense,
          id: "cccccccc-cccc-cccc-cccc-cccccccccccc",
          description: "September food",
          category: "Food & Dining",
          amount: 50000,
          expenseDate: "2026-09-20",
        },
      ]);

      const wrapper = mount(App);
      await flushPromises();

      expect(wrapper.text().replace(/\s/g, "")).toContain("Rp50.000");

      const categorySelect = wrapper.find("select");
      expect(categorySelect.exists()).toBe(true);

      await categorySelect.setValue("Food & Dining");

      expect(wrapper.text()).toContain("October food");
      expect(wrapper.text()).not.toContain("October transport");
      expect(wrapper.text()).not.toContain("September food");

      // The period total should not change when only the category filter changes.
      expect(wrapper.text().replace(/\s/g, "")).toContain("Rp50.000");

      wrapper.unmount();
    } finally {
      vi.useRealTimers();
    }
  });

  it("shows a validation message when the date range is invalid", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 9, 29, 12));

    try {
      vi.mocked(getExpenses).mockResolvedValue([sampleExpense]);

      const wrapper = mount(App);
      await flushPromises();

      const customRangeButton = wrapper
        .findAll("button")
        .find((button) => button.text().trim() === "Custom range");

      expect(customRangeButton).toBeDefined();
      await customRangeButton!.trigger("click");

      await wrapper
        .get('input[aria-label="Start date"]')
        .setValue("2026-10-25");

      await wrapper.get('input[aria-label="End date"]').setValue("2026-10-10");

      expect(wrapper.get('[role="alert"]').text()).toContain(
        "Start date must be on or before the end date.",
      );

      wrapper.unmount();
    } finally {
      vi.useRealTimers();
    }
  });

  it("parses a chat message and displays an expense preview", async () => {
    const wrapper = mount(App);
    await flushPromises();

    const chatDemoButton = wrapper
      .findAll("button")
      .find((button) => button.text().includes("Chat Demo"));

    expect(chatDemoButton).toBeDefined();
    await chatDemoButton!.trigger("click");

    await wrapper
      .get('input[aria-label="Chat message"]')
      .setValue("Makan siang 35000");

    await wrapper.get("form.chat-input").trigger("submit");

    expect(wrapper.text()).toContain("Review expense");
    expect(
      (wrapper.get('input[type="number"]').element as HTMLInputElement).value,
    ).toBe("35000");
    expect(wrapper.text()).toContain("Makan siang");
    expect(createExpense).not.toHaveBeenCalled();

    wrapper.unmount();
  });

  it.each([
    ["Makan siang", "missing amount"],
    ["Makan siang 0", "zero amount"],
    ["Makan siang abc", "non-numeric amount"],
    ["Makan siang -35000", "negative amount"],
    ["Makan siang 9007199254740992", "unsafe integer"],
  ])("rejects invalid chat message: %s (%s)", async (message) => {
    const wrapper = mount(App);
    await flushPromises();

    const chatDemoButton = wrapper
      .findAll("button")
      .find((button) => button.text().includes("Chat Demo"));

    expect(chatDemoButton).toBeDefined();
    await chatDemoButton!.trigger("click");

    await wrapper.get('input[aria-label="Chat message"]').setValue(message);

    await wrapper.get("form.chat-input").trigger("submit");

    expect(wrapper.text()).not.toContain("Review expense");
    expect(createExpense).not.toHaveBeenCalled();

    wrapper.unmount();
  });

  it("saves a chat expense only after confirmation", async () => {
    const savedExpense = {
      ...sampleExpense,
      id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee",
      amount: 35000,
      category: "Other",
      description: "Makan siang",
      expenseDate: new Date().toLocaleDateString("en-CA"),
    };

    vi.mocked(createExpense).mockResolvedValue(savedExpense);

    const wrapper = mount(App);
    await flushPromises();

    const chatDemoButton = wrapper
      .findAll("button")
      .find((button) => button.text().includes("Chat Demo"));

    expect(chatDemoButton).toBeDefined();
    await chatDemoButton!.trigger("click");

    await wrapper
      .get('input[aria-label="Chat message"]')
      .setValue("Makan siang 35000");
    await wrapper.get("form.chat-input").trigger("submit");

    expect(createExpense).not.toHaveBeenCalled();

    await wrapper.get("button.primary-button").trigger("click");
    await flushPromises();

    expect(wrapper.get('[role="status"]').text()).toContain(
      "Expense saved: Makan siang",
    );
    expect(wrapper.get('[role="status"]').text()).toMatch(/Rp\s*35\.000/);

    expect(createExpense).toHaveBeenCalledWith({
      amount: 35000,
      category: "Other",
      description: "Makan siang",
      expenseDate: expect.any(String),
    });

    expect(wrapper.text()).toContain("Makan siang");
    expect(wrapper.find("form.chat-input").exists()).toBe(true);

    wrapper.unmount();
  });

  it("keeps the chat expense preview when saving fails", async () => {
    vi.mocked(createExpense).mockRejectedValue(new Error("API unavailable"));

    const wrapper = mount(App);
    await flushPromises();

    const chatDemoButton = wrapper
      .findAll("button")
      .find((button) => button.text().includes("Chat Demo"));

    expect(chatDemoButton).toBeDefined();
    await chatDemoButton!.trigger("click");

    await wrapper
      .get('input[aria-label="Chat message"]')
      .setValue("Makan siang 35000");

    await wrapper.get("form.chat-input").trigger("submit");

    expect(wrapper.text()).toContain("Review expense");

    await wrapper.get("button.primary-button").trigger("click");
    await flushPromises();

    // Error should be displayed.
    expect(wrapper.get('[role="alert"]').text()).toContain("API unavailable");

    // Draft should remain available for correction or retry.
    expect(wrapper.text()).toContain("Review expense");
    expect(
      (wrapper.get('input[type="number"]').element as HTMLInputElement).value,
    ).toBe("35000");
    expect(wrapper.text()).toContain("Makan siang");

    // Success message should not appear.
    expect(wrapper.find('[role="status"]').exists()).toBe(false);

    // API was called once and the draft was not discarded.
    expect(createExpense).toHaveBeenCalledTimes(1);

    wrapper.unmount();
  });

  it("displays zero values and empty state when there are no expenses", async () => {
    vi.mocked(getExpenses).mockResolvedValue([]);

    const wrapper = mount(App);
    await flushPromises();

    const summaryCards = wrapper.findAll(".summary-card");

    expect(summaryCards).toHaveLength(4);

    expect(summaryCards[0].text().replace(/\s/g, "")).toContain("Rp0");

    expect(summaryCards[1].text()).toContain("0");

    expect(summaryCards[2].text().replace(/\s/g, "")).toContain("Rp0");

    expect(summaryCards[3].text()).toContain("—");

    expect(wrapper.text()).toContain(
      "Add an expense to see your category breakdown.",
    );

    expect(wrapper.findAll(".category-row")).toHaveLength(0);

    wrapper.unmount();
  });
});
