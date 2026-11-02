import { computed, ref } from "vue";

export type DateRangePreset = "this-month" | "last-month" | "custom";

export function toDateInputValue(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");

  return `${year}-${month}-${day}`;
}

function getMonthStart(date: Date): string {
  return toDateInputValue(new Date(date.getFullYear(), date.getMonth(), 1));
}

function getMonthEnd(date: Date): string {
  return toDateInputValue(new Date(date.getFullYear(), date.getMonth() + 1, 0));
}

export function useDateRange() {
  const today = new Date();

  const dateRangePreset = ref<DateRangePreset>("this-month");
  const startDate = ref(getMonthStart(today));
  const endDate = ref(toDateInputValue(today));

  const lastMonthDate = new Date(today.getFullYear(), today.getMonth() - 1, 1);

  const lastMonthStart = getMonthStart(lastMonthDate);
  const lastMonthEnd = getMonthEnd(lastMonthDate);

  function selectDateRange(preset: DateRangePreset) {
    dateRangePreset.value = preset;

    if (preset === "this-month") {
      const now = new Date();
      startDate.value = getMonthStart(now);
      endDate.value = toDateInputValue(now);
    } else if (preset === "last-month") {
      startDate.value = lastMonthStart;
      endDate.value = lastMonthEnd;
    }
  }

  const isDateRangeValid = computed(() => startDate.value <= endDate.value);

  return {
    dateRangePreset,
    startDate,
    endDate,
    selectDateRange,
    isDateRangeValid,
  };
}
