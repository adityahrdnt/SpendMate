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

  it("shows an error when loading expenses fails", async () => {
    vi.mocked(getExpenses).mockRejectedValue(new Error("API unavailable"));

    const wrapper = mount(App);
    await flushPromises();

    expect(wrapper.text()).toContain("Unable to load expenses");
    expect(wrapper.text()).toContain("API unavailable");
    expect(wrapper.find('[role="alert"]').exists()).toBe(true);

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
});
