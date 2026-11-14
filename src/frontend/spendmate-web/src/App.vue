<script setup lang="ts">
  import { useDashboardMetrics } from './composables/useDashboardMetrics'
  import {
    toDateInputValue,
    useDateRange,
  } from './composables/useDateRange'
  import { useAuth } from './composables/useAuth'
  import AuthView from './components/AuthView.vue'
  import type { LoginRequest, RegisterRequest } from './types/auth'
  import type { Expense } from './types/expense'
  import { useExpenses } from './composables/useExpenses'
  import { useFamilies } from './composables/useFamilies'
  import { formatCurrency, formatDate } from './utils/formatters'
  import { computed, onMounted, ref } from 'vue'

  type Page = 'Dashboard' | 'Expenses' | 'Chat Demo'

  const activePage = ref<Page>('Dashboard')
  const selectedFilter = ref('All')

  // Authentication
  const {
    user,
    status: authStatus,
    errorMessage: authErrorMessage,
    initialize,
    refreshCurrentUser,
    login,
    register,
    logout,
  } = useAuth()

  const isAuthSubmitting = ref(false)

  const {
    families,
    isLoadingFamilies,
    isSwitchingFamily,
    isCreatingFamily,
    errorMessage: familyErrorMessage,
    loadFamilies,
    switchActiveFamily,
    createFamily,
  } = useFamilies()

  const isFamilyMenuOpen = ref(false)

  const familyId = computed(() => user.value?.familyId ?? '')
  const currentUserId = computed(() => user.value?.userId ?? '')
  const displayName = computed(() => user.value?.displayName ?? '')
  const familyName = computed(() => user.value?.familyName ?? '')
  const isCreateFamilyFormOpen = ref(false)
  const newFamilyName = ref('')

  const userInitial = computed(() =>
    displayName.value.trim().charAt(0).toUpperCase() || '?',
  )

  const familyInitial = computed(() =>
    familyName.value.trim().charAt(0).toUpperCase() || '?',
  )

  async function handleLogin(request: LoginRequest) {
    isAuthSubmitting.value = true

    try {
      const success = await login(request)

      if (success) {
        await Promise.all([
          loadFamilies(),
          loadExpenses(),
        ])
      }
    } finally {
      isAuthSubmitting.value = false
    }
  }

  async function handleRegister(request: RegisterRequest) {
    isAuthSubmitting.value = true

    try {
      const success = await register(request)

      if (success) {
        await Promise.all([
          loadFamilies(),
          loadExpenses(),
        ])
      }
    } finally {
      isAuthSubmitting.value = false
    }
  }

  async function handleLogout() {
    const success = await logout()

    if (success) {
      expenses.value = []
      families.value = []
      isFamilyMenuOpen.value = false
      activePage.value = 'Dashboard'
      selectedFilter.value = 'All'
    }
  }

  async function handleSwitchFamily(selectedFamilyId: string) {
    if (
      selectedFamilyId === familyId.value
      || isSwitchingFamily.value
    ) {
      isFamilyMenuOpen.value = false
      return
    }

    const success = await switchActiveFamily(selectedFamilyId)

    if (!success) return

    const refreshed = await refreshCurrentUser()

    if (!refreshed) {
      familyErrorMessage.value =
        authErrorMessage.value
        || 'Family changed, but the current user could not be refreshed. Please try again.'
      return
    }

    await loadExpenses()
    isFamilyMenuOpen.value = false
  }

  async function handleCreateFamily() {
    const name = newFamilyName.value.trim()

    if (!name || isCreatingFamily.value) {
      return
    }

    const success = await createFamily(name)

    if (!success) {
      return
    }

    newFamilyName.value = ''
    isCreateFamilyFormOpen.value = false
  }

  // Expenses
  const {
    expenses,
    isLoading,
    errorMessage,
    isSaving,
    saveErrorMessage,
    deletingExpenseId,
    deleteErrorMessage,
    loadExpenses,
    saveExpense,
    removeExpense,
  } = useExpenses(familyId, currentUserId)

  const {
    dateRangePreset,
    startDate,
    endDate,
    selectDateRange,
    isDateRangeValid,
  } = useDateRange()

  const currentMonthLabel = computed(() =>
    new Intl.DateTimeFormat('en-US', {
      month: 'long',
      year: 'numeric',
    }).format(new Date()),
  )

  const currentDateGreeting = computed(() =>
    new Intl.DateTimeFormat('en-US', {
      weekday: 'long',
      month: 'long',
      day: 'numeric',
      year: 'numeric',
    })
      .format(new Date())
      .toUpperCase(),
  )

  const dateRangeActivityLabel = computed(() => {
    if (dateRangePreset.value === 'this-month') return 'This month'
    if (dateRangePreset.value === 'last-month') return 'Last month'
    return 'Selected period'
  })

  const dateFilteredExpenses = computed(() => {
    if (!isDateRangeValid.value) return []

    return expenses.value.filter(
      expense =>
        expense.date >= startDate.value
        && expense.date <= endDate.value,
    )
  })

  const isExpenseModalOpen = ref(false)
  const editingExpenseId = ref<string | null>(null)

  const categories = [
    { name: 'Groceries', color: '#3c8c70', icon: '🛒' },
    { name: 'Food & Dining', color: '#e9a23b', icon: '🍽️' },
    { name: 'Transport', color: '#6587d8', icon: '🚗' },
    { name: 'Bills', color: '#a47bc2', icon: '🧾' },
    { name: 'Other', color: '#9ca3af', icon: '📦' },
  ]

  const {
    totalSpent,
    transactionCount,
    averageExpense,
    expenseCategories,
    categoryTotals,
    largestCategory,
  } = useDashboardMetrics(dateFilteredExpenses, categories)

  type ChatExpenseDraft = {
    description: string
    amount: number
    category: string
    date: string
  }

  const chatMessage = ref('')
  const chatErrorMessage = ref('')
  const chatSuccessMessage = ref('')
  const chatExpenseDraft = ref<ChatExpenseDraft | null>(null)
  const isSavingChatExpense = ref(false)

  function parseChatExpense(message: string): ChatExpenseDraft | null {
    const match = message.trim().match(/^(.+?)\s+([\d.,]+)$/)

    if (!match) return null

    const description = match[1].trim()
    const amountText = match[2].replace(/[.,]/g, '')

    if (!description || !/^\d+$/.test(amountText)) return null

    const amount = Number(amountText)

    if (!Number.isSafeInteger(amount) || amount <= 0) return null

    return {
      description,
      amount,
      category: 'Other',
      date: toDateInputValue(new Date()),
    }
  }

  function submitChatMessage() {
    chatErrorMessage.value = ''
    chatSuccessMessage.value = ''
    chatExpenseDraft.value = null

    const draft = parseChatExpense(chatMessage.value)

    if (!draft) {
      chatErrorMessage.value =
        'Enter a description followed by a positive whole amount, e.g. Makan siang 35000.'
      return
    }

    chatExpenseDraft.value = draft
  }

  async function confirmChatExpense() {
    const draft = chatExpenseDraft.value

    if (
      !draft
      || !draft.description.trim()
      || !Number.isSafeInteger(draft.amount)
      || draft.amount <= 0
      || !draft.date
      || isSavingChatExpense.value
    ) {
      chatErrorMessage.value =
        'Enter a valid description, positive whole amount, and date.'
      return
    }

    isSavingChatExpense.value = true
    chatErrorMessage.value = ''

    try {
      const savedExpense = await saveExpense({
        description: draft.description.trim(),
        amount: draft.amount,
        category: draft.category,
        expenseDate: draft.date,
      })

      if (!savedExpense) {
        chatErrorMessage.value =
          saveErrorMessage.value || 'Unable to save the expense. Please try again.'
        return
      }

      chatSuccessMessage.value =
        `Expense saved: ${draft.description.trim()} — ${formatCurrency(draft.amount)}`

      chatMessage.value = ''
      chatExpenseDraft.value = null
    } finally {
      isSavingChatExpense.value = false
    }
  }

  // Initialize authentication before loading expenses.
  onMounted(async () => {
    await initialize()

    if (authStatus.value === 'authenticated') {
      await Promise.all([
        loadFamilies(),
        loadExpenses(),
      ])
    }
  })

  const filteredExpenses = computed(() => {
    const sorted = [...dateFilteredExpenses.value].sort(
      (a, b) =>
        b.date.localeCompare(a.date)
        || b.createdAtUtc.localeCompare(a.createdAtUtc),
    )

    if (selectedFilter.value === 'All') return sorted

    return sorted.filter(
      expense => expense.category === selectedFilter.value,
    )
  })

  const newExpense = ref({
    description: '',
    category: 'Food & Dining',
    amount: '',
    date: toDateInputValue(new Date()),
  })

  function openAddExpenseModal() {
    editingExpenseId.value = null
    saveErrorMessage.value = ''
    newExpense.value = {
      description: '',
      category: 'Food & Dining',
      amount: '',
      date: toDateInputValue(new Date()),
    }
    isExpenseModalOpen.value = true
  }

  function openEditExpenseModal(expense: Expense) {
    editingExpenseId.value = expense.id
    saveErrorMessage.value = ''
    newExpense.value = {
      description: expense.description,
      category: expense.category,
      amount: String(expense.amount),
      date: expense.date,
    }
    isExpenseModalOpen.value = true
  }

  async function addExpense() {
    const amount = Number(newExpense.value.amount)
    const description = newExpense.value.description.trim()

    if (
      !description
      || !Number.isFinite(amount)
      || amount <= 0
      || !Number.isInteger(amount)
      || !newExpense.value.date
    ) {
      saveErrorMessage.value =
        'Enter a valid description, positive whole amount, and date.'
      return
    }

    const request = {
      amount,
      category: newExpense.value.category,
      description,
      expenseDate: newExpense.value.date,
    }

    const savedExpense = await saveExpense(
      request,
      editingExpenseId.value ?? undefined,
    )

    if (!savedExpense) return

    newExpense.value = {
      description: '',
      category: 'Food & Dining',
      amount: '',
      date: toDateInputValue(new Date()),
    }

    editingExpenseId.value = null
    isExpenseModalOpen.value = false
  }

  async function deleteExpense(expense: Expense) {
    const confirmed = window.confirm(
      `Delete "${expense.description}"? This action cannot be undone.`,
    )

    if (!confirmed) return

    await removeExpense(expense.id)
  }
</script>


<template>
  <div
    v-if="authStatus === 'initializing'"
    class="auth-loading"
    role="status"
  >
    Checking your session...
  </div>

  <AuthView
    v-else-if="authStatus === 'unauthenticated'"
    :is-submitting="isAuthSubmitting"
    :error-message="authErrorMessage"
    @login="handleLogin"
    @register="handleRegister"
  />

  <div v-else class="app-shell">
    <aside class="sidebar">
      <a class="brand" href="#" @click.prevent="activePage = 'Dashboard'">
        <span class="brand-mark">S</span>
        <span>spend<span class="brand-light">mate</span></span>
      </a>

      <div class="workspace-switcher">
        <button
          type="button"
          class="workspace"
          :aria-expanded="isFamilyMenuOpen"
          aria-haspopup="true"
          :disabled="isLoadingFamilies || isSwitchingFamily"
          @click="isFamilyMenuOpen = !isFamilyMenuOpen"
        >
          <div class="workspace-avatar">{{ familyInitial }}</div>

          <div class="workspace-copy">
            <span class="eyebrow">WORKSPACE</span>
            <strong>{{ familyName }}</strong>
          </div>

          <span class="chevron">
            {{ isFamilyMenuOpen ? '⌃' : '⌄' }}
          </span>
        </button>

        <div
          v-if="isFamilyMenuOpen"
          class="family-menu"
          role="menu"
          aria-label="Switch family"
        >

          <div class="family-menu-heading">Switch workspace</div>

          <div v-if="isLoadingFamilies" class="family-menu-message">
            Loading families...
          </div>

          <template v-else>
            <button
              v-for="family in families"
              :key="family.id"
              type="button"
              class="family-menu-item"
              :class="{ active: family.id === familyId }"
              role="menuitem"
              :disabled="isSwitchingFamily || isCreatingFamily || family.id === familyId"
              @click="handleSwitchFamily(family.id)"
            >
              <span class="family-menu-avatar">
                {{ family.name.charAt(0).toUpperCase() }}
              </span>

              <span class="family-menu-copy">
                <strong>{{ family.name }}</strong>
                <small>{{ family.type }} · {{ family.role }}</small>
              </span>

              <span
                v-if="family.id === familyId"
                class="family-menu-check"
                aria-label="Active family"
              >
                ✓
              </span>
            </button>

            <div v-if="families.length === 0" class="family-menu-message">
              No families available.
            </div>
          </template>

          <div class="family-menu-divider"></div>

          <button
            v-if="!isCreateFamilyFormOpen"
            type="button"
            class="family-menu-create-button"
            :disabled="isCreatingFamily || isSwitchingFamily"
            @click="isCreateFamilyFormOpen = true"
          >
            <span>+</span>
            Create Shared Family
          </button>

          <form
            v-else
            class="family-create-form"
            @submit.prevent="handleCreateFamily"
          >
            <label for="new-family-name">Family name</label>

            <input
              id="new-family-name"
              v-model="newFamilyName"
              type="text"
              maxlength="100"
              placeholder="e.g. Herdianto Shared Family"
              required
              :disabled="isCreatingFamily"
            />

            <div class="family-create-actions">
              <button
                type="button"
                class="family-create-cancel"
                :disabled="isCreatingFamily"
                @click="isCreateFamilyFormOpen = false; newFamilyName = ''"
              >
                Cancel
              </button>

              <button
                type="submit"
                class="family-create-submit"
                :disabled="!newFamilyName.trim() || isCreatingFamily"
              >
                {{ isCreatingFamily ? 'Creating...' : 'Create' }}
              </button>
            </div>
          </form>


          <div
            v-if="familyErrorMessage"
            class="family-menu-error"
            role="alert"
          >
            {{ familyErrorMessage }}
          </div>

          <div v-if="isSwitchingFamily" class="family-menu-message">
            Switching workspace...
          </div>
        </div>
      </div>

      <span class="nav-label">MENU</span>
      <nav class="main-nav" aria-label="Main navigation">
        <button
          v-for="item in [
            { name: 'Dashboard' as Page, icon: '▦' },
            { name: 'Expenses' as Page, icon: '↗' },
            { name: 'Chat Demo' as Page, icon: '☏' },
          ]"
          :key="item.name"
          class="nav-item"
          :class="{ active: activePage === item.name }"
          @click="activePage = item.name"
        >
          <span class="nav-icon">{{ item.icon }}</span>
          {{ item.name }}
          <span v-if="item.name === 'Chat Demo'" class="demo-tag">DEMO</span>
        </button>
      </nav>

      <div class="sidebar-bottom">
        <div class="family-card">
          <div class="family-avatars">
            <span class="person-avatar avatar-one">{{ userInitial }}</span>
          </div>
          <strong>Better together.</strong>
          <p>Keep your family spending in sync.</p>
          <button @click="activePage = 'Expenses'">
            View family activity <span>→</span>
          </button>
        </div>

        <div class="profile">
          <div class="profile-avatar">{{ userInitial }}</div>
          <div class="profile-copy">
            <strong>{{ displayName }}</strong>
            <span>Signed in</span>
          </div>
          <button
            type="button"
            class="more-icon"
            title="Sign out"
            aria-label="Sign out"
            @click="handleLogout"
          >
            ↪
          </button>
        </div>
      </div>
    </aside>

    <main class="main-content">
      <header class="topbar">
        <div class="breadcrumb">
          <span>Workspace</span><span class="breadcrumb-separator">/</span>
          <strong>{{ activePage }}</strong>
        </div>
        <div class="topbar-actions">
          <span class="current-month">{{ currentMonthLabel }}</span>
          <button class="avatar-small" aria-label="User profile">{{ userInitial }}</button>
        </div>
      </header>

      <div class="page-content">
        <template v-if="activePage !== 'Chat Demo'">
          <div v-if="isLoading" class="empty-state" role="status">
            Loading family expenses...
          </div>

          <div v-else-if="errorMessage" class="empty-state" role="alert">
            <p>Unable to load expenses: {{ errorMessage }}</p>
            <button class="secondary-button" @click="loadExpenses">
              Try again
            </button>
          </div>

          <template v-else>
            <section class="page-heading">
              <div>
                <div class="greeting">{{ currentDateGreeting }}</div>
                <h1>{{ activePage === 'Dashboard' ? 'Your family at a glance' : 'All expenses' }}</h1>
                <p class="subtitle">
                  {{ activePage === 'Dashboard'
                    ? 'A clearer picture of your money, together.'
                    : 'Keep track of every expense shared by your family.' }}
                </p>
              </div>
              <button
                class="primary-button"
                @click="openAddExpenseModal()"
              >
                <span class="plus-icon">+</span> Add expense
              </button>
            </section>

            <section class="date-filter-panel" aria-label="Filter expenses by date">
              <div class="date-filter-presets">
                <button
                  type="button"
                  class="secondary-button"
                  :class="{ 'date-preset-active': dateRangePreset === 'this-month' }"
                  @click="selectDateRange('this-month')"
                >
                  This month
                </button>

                <button
                  type="button"
                  class="secondary-button"
                  :class="{ 'date-preset-active': dateRangePreset === 'last-month' }"
                  @click="selectDateRange('last-month')"
                >
                  Last month
                </button>

                <button
                  type="button"
                  class="secondary-button"
                  :class="{ 'date-preset-active': dateRangePreset === 'custom' }"
                  @click="selectDateRange('custom')"
                >
                  Custom range
                </button>
              </div>

              <div v-if="dateRangePreset === 'custom'" class="date-filter-inputs">
                <label>
                  From
                  <input
                    v-model="startDate"
                    type="date"
                    aria-label="Start date"
                  />
                </label>

                <label>
                  To
                  <input
                    v-model="endDate"
                    type="date"
                    aria-label="End date"
                  />
                </label>
              </div>

              <p v-if="!isDateRangeValid" class="date-filter-error" role="alert">
                Start date must be on or before the end date.
              </p>
            </section>

            <section v-if="activePage === 'Dashboard'" class="summary-grid">
              <article class="summary-card total-card">
                <div class="card-topline">
                  <span class="summary-label">Total expenses</span>
                  <span class="summary-icon green-icon">↗</span>
                </div>
                <div class="summary-amount">{{ formatCurrency(totalSpent) }}</div>
                <div class="summary-foot">
                  <span class="status-dot"></span>
                  Spending in selected period
                </div>
                <div class="card-decoration decoration-one"></div>
              </article>

              <article class="summary-card">
                <div class="card-topline">
                  <span class="summary-label">Transactions</span>
                  <span class="summary-icon blue-icon">≡</span>
                </div>
                <div class="summary-amount">{{ transactionCount }}</div>
                <div class="summary-foot">Transactions in selected period</div>
              </article>

              <article class="summary-card">
                <div class="card-topline">
                  <span class="summary-label">Average expense</span>
                  <span class="summary-icon amber-icon">◷</span>
                </div>
                <div class="summary-amount">
                  {{ formatCurrency(averageExpense) }}
                </div>
                <div class="summary-foot">Per transaction</div>
              </article>

              <article class="summary-card">
                <div class="card-topline">
                  <span class="summary-label">Top category</span>
                  <span class="summary-icon purple-icon">◇</span>
                </div>
                <div class="summary-amount category-highlight">{{ largestCategory }}</div>
                <div class="summary-foot">By total spending</div>
              </article>
            </section>

            <section v-if="activePage === 'Dashboard'" class="content-grid">
              <article class="panel category-panel">
                <div class="panel-heading">
                  <div>
                    <h2>Spending by category</h2>
                    <p class="panel-subtitle">Where your money goes</p>
                  </div>
                  <button class="text-button" @click="activePage = 'Expenses'">View all <span>→</span></button>
                </div>

                <div v-if="categoryTotals.length" class="category-list">
                  <div v-for="category in categoryTotals" :key="category.name" class="category-row">
                    <div class="category-icon" :style="{ backgroundColor: `${category.color}18` }">
                      {{ category.icon }}
                    </div>
                    <div class="category-info">
                      <div class="category-label-row">
                        <span class="category-name">{{ category.name }}</span>
                        <strong>{{ formatCurrency(category.amount) }}</strong>
                      </div>
                      <div class="progress-track">
                        <div
                          class="progress-bar"
                          :style="{
                            width: `${totalSpent ? category.amount / totalSpent * 100 : 0}%`,
                            backgroundColor: category.color,
                          }"
                        ></div>
                      </div>
                    </div>
                  </div>
                </div>
                <p v-else class="empty-state">Add an expense to see your category breakdown.</p>
                <div class="category-total">
                  <span>Total across categories</span>
                  <strong>{{ formatCurrency(totalSpent) }}</strong>
                </div>
              </article>

              <article class="panel activity-panel">
                <div class="panel-heading">
                  <div>
                    <h2>Family activity</h2>
                    <p class="panel-subtitle">Who added what</p>
                  </div>
                  <span class="live-label"><span></span> {{ dateRangeActivityLabel }}</span>
                </div>

                <div class="activity-list">
                  <div v-for="expense in dateFilteredExpenses.slice(0, 4)" :key="expense.id" class="activity-row">
                    <div class="activity-avatar" :class="expense.createdBy === 'You' ? 'you-avatar' : 'sarah-avatar'">
                      {{ expense.createdBy === 'You' ? 'A' : 'S' }}
                    </div>
                    <div class="activity-info">
                      <strong>{{ expense.createdBy }}</strong>
                      <span>added {{ expense.description.toLowerCase() }}</span>
                      <small>{{ formatDate(expense.date) }}</small>
                    </div>
                    <strong class="activity-amount">{{ formatCurrency(expense.amount) }}</strong>
                  </div>
                </div>
                <div class="activity-footer">
                  <span class="member-indicator"></span>
                  <span>2 family members contributing</span>
                </div>
              </article>
            </section>

            <section class="panel transactions-panel">
              <div class="panel-heading transaction-heading">
                <div>
                  <h2>{{ activePage === 'Dashboard' ? 'Recent transactions' : 'Expense history' }}</h2>
                  <p class="panel-subtitle">
                    {{ activePage === 'Dashboard' ? 'Your latest family expenses' : `${filteredExpenses.length} transaction(s)` }}
                  </p>
                </div>
                <div class="transaction-actions">
                  <select v-model="selectedFilter" aria-label="Filter by category" class="filter-select">
                    <option>All</option>
                    <option v-for="category in expenseCategories" :key="category.name" :value="category.name">
                      {{ category.name }}
                    </option>
                  </select>
                  <button v-if="activePage === 'Dashboard'" class="text-button desktop-only" @click="activePage = 'Expenses'">
                    View all <span>→</span>
                  </button>
                </div>
              </div>

              <!-- Desktop / tablet: existing table layout -->
              <div class="table-wrap desktop-transactions">
                <table>
                  <thead>
                    <tr>
                      <th>DESCRIPTION</th>
                      <th>CATEGORY</th>
                      <th>DATE</th>
                      <th>ADDED BY</th>
                      <th class="amount-column">AMOUNT</th>
                      <th>ACTIONS</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr
                      v-for="expense in (activePage === 'Dashboard' ? filteredExpenses.slice(0, 5) : filteredExpenses)"
                      :key="expense.id"
                    >
                      <td>
                        <div class="expense-description">
                          <span class="expense-bullet" :class="{ 'bullet-food': expense.category === 'Food & Dining' }">
                            {{ categories.find(category => category.name === expense.category)?.icon ?? '📦' }}
                          </span>
                          <strong>{{ expense.description }}</strong>
                        </div>
                      </td>
                      <td><span class="category-pill">{{ expense.category }}</span></td>
                      <td class="date-cell">{{ formatDate(expense.date) }}</td>
                      <td>
                        <span class="added-by">
                          <span class="mini-avatar" :class="expense.createdBy === 'You' ? 'you-avatar' : 'sarah-avatar'">
                            {{ expense.createdBy === 'You' ? 'A' : 'S' }}
                          </span>
                          {{ expense.createdBy }}
                        </span>
                      </td>
                      <td class="amount-column amount-cell">{{ formatCurrency(expense.amount) }}</td>
                      <td class="actions-cell">
                        <button type="button" class="secondary-button" @click="openEditExpenseModal(expense)">
                          Edit
                        </button>
                        <button
                          type="button"
                          class="secondary-button"
                          :disabled="deletingExpenseId === expense.id"
                          @click="deleteExpense(expense)"
                        >
                          {{ deletingExpenseId === expense.id ? 'Deleting...' : 'Delete' }}
                        </button>
                      </td>
                    </tr>
                    <tr v-if="filteredExpenses.length === 0">
                      <td colspan="6" class="empty-table">
                        No expenses found for the selected filters.
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>

              <!-- Mobile: card layout -->
              <div class="mobile-transactions">
                <article
                  v-for="expense in (activePage === 'Dashboard' ? filteredExpenses.slice(0, 5) : filteredExpenses)"
                  :key="expense.id"
                  class="expense-mobile-card"
                >
                  <div class="expense-mobile-top">
                    <div class="expense-description">
                      <span class="expense-bullet" :class="{ 'bullet-food': expense.category === 'Food & Dining' }">
                        {{ categories.find(category => category.name === expense.category)?.icon ?? '📦' }}
                      </span>
                      <div class="expense-mobile-info">
                        <strong>{{ expense.description }}</strong>
                        <span class="category-pill">{{ expense.category }}</span>
                      </div>
                    </div>

                    <strong class="amount-cell">{{ formatCurrency(expense.amount) }}</strong>
                  </div>

                  <div class="expense-mobile-meta">
                    <span class="date-cell">{{ formatDate(expense.date) }}</span>
                    <span class="added-by">
                      <span class="mini-avatar" :class="expense.createdBy === 'You' ? 'you-avatar' : 'sarah-avatar'">
                        {{ expense.createdBy === 'You' ? 'A' : 'S' }}
                      </span>
                      {{ expense.createdBy }}
                    </span>
                  </div>

                  <div class="expense-mobile-actions">
                    <button
                      type="button"
                      class="secondary-button"
                      @click="openEditExpenseModal(expense)"
                    >
                      Edit
                    </button>
                    <button
                      type="button"
                      class="secondary-button"
                      :disabled="deletingExpenseId === expense.id"
                      @click="deleteExpense(expense)"
                    >
                      {{ deletingExpenseId === expense.id ? 'Deleting...' : 'Delete' }}
                    </button>
                  </div>
                </article>

                <p v-if="filteredExpenses.length === 0" class="mobile-empty-state">
                  No expenses found for the selected filters.
                </p>
              </div>

              <p v-if="deleteErrorMessage" class="empty-state" role="alert">
                {{ deleteErrorMessage }}
              </p>

            </section>

            <footer class="page-footer">
              <span>Made for the things that matter.</span>
              <span>SpendMate <span class="footer-dot">•</span> MVP preview</span>
            </footer>
          </template>
        </template>

        <section v-else class="chat-demo-page">
          <div class="page-heading">
            <div>
              <div class="greeting">QUICK CAPTURE</div>
              <h1>Chat Demo</h1>
              <p class="subtitle">Try adding an expense using a simple chat message.</p>
            </div>
          </div>
          <div class="chat-card">
            <div class="chat-header">
              <div class="chat-avatar">S</div>
              <div><strong>SpendMate Assistant</strong><span>Simulated chat · Demo only</span></div>
              <span class="demo-tag">DEMO</span>
            </div>
            <div class="chat-messages">
              <div class="chat-date">TODAY</div>
              <div class="message assistant-message">
                Hi! Type an expense, for example
                <strong>Makan siang 35000</strong>.
                Review the details before saving.
              </div>

              <div v-if="chatMessage.trim()" class="message user-message">
                {{ chatMessage }}
              </div>

              <p v-if="chatErrorMessage" class="empty-state" role="alert">
                {{ chatErrorMessage }}
              </p>

              <p v-if="chatSuccessMessage" class="message assistant-message" role="status">
                {{ chatSuccessMessage }}
              </p>

              <div v-if="chatExpenseDraft" class="message assistant-message">
                <strong>Review expense</strong>

                <label>
                  Description
                  <input v-model="chatExpenseDraft.description" required maxlength="500" />
                </label>

                <label>
                  Amount (IDR)
                  <input
                    v-model.number="chatExpenseDraft.amount"
                    type="number"
                    min="1"
                    step="1"
                    required
                  />
                </label>

                <label>
                  Category
                  <select v-model="chatExpenseDraft.category" required>
                    <option
                      v-for="category in categories"
                      :key="category.name"
                      :value="category.name"
                    >
                      {{ category.name }}
                    </option>
                  </select>
                </label>

                <label>
                  Expense date
                  <input v-model="chatExpenseDraft.date" type="date" required />
                </label>

                <p>{{ formatCurrency(chatExpenseDraft.amount) }}</p>

                <button
                  type="button"
                  class="primary-button"
                  :disabled="isSavingChatExpense"
                  @click="confirmChatExpense"
                >
                  {{ isSavingChatExpense ? 'Saving...' : 'Confirm and save' }}
                </button>
              </div>
            </div>

            <form class="chat-input" @submit.prevent="submitChatMessage">
              <input
                v-model="chatMessage"
                aria-label="Chat message"
                placeholder="e.g. Makan siang 35000"
                required
              />
              <button type="submit" aria-label="Parse message">↑</button>
            </form>
          </div>
        </section>
      </div>
    </main>

    <div v-if="isExpenseModalOpen" class="modal-backdrop" @click.self="isExpenseModalOpen = false">
      <section class="expense-modal" role="dialog" aria-modal="true" aria-labelledby="modal-title">
        <div class="modal-heading">
          <div>
            <span class="greeting">FAMILY FINANCES</span>
            <h2 id="modal-title">
              {{ editingExpenseId ? 'Edit expense' : 'Add an expense' }}
            </h2>
            <p>
              {{ editingExpenseId
                ? 'Update the details of this family transaction.'
                : 'Record a new family transaction.' }}
            </p>
          </div>
          <button class="close-button" aria-label="Close dialog" @click="isExpenseModalOpen = false">×</button>
        </div>
        <form class="expense-form" @submit.prevent="addExpense">
          <label>
            Description
            <input v-model="newExpense.description" required maxlength="100" placeholder="e.g. Weekly groceries" />
          </label>
          <label>
            Amount (IDR)
            <input v-model="newExpense.amount" required type="number" min="1" step="1" placeholder="150000" />
          </label>
          <label>
            Category
            <select v-model="newExpense.category" required>
              <option v-for="category in categories" :key="category.name" :value="category.name">
                {{ category.name }}
              </option>
            </select>
          </label>
          <label>
            Expense date
            <input v-model="newExpense.date" required type="date" />
          </label>
          <p v-if="saveErrorMessage" class="empty-state" role="alert">
            {{ saveErrorMessage }}
          </p>
          <div class="modal-actions">
            <button type="button" class="secondary-button" @click="isExpenseModalOpen = false">Cancel</button>
            <button type="submit" class="primary-button" :disabled="isSaving">
              {{ isSaving
                ? 'Saving...'
                : editingExpenseId
                  ? 'Save changes'
                  : 'Save expense' }}
            </button>
          </div>
        </form>
      </section>
    </div>
  </div>
</template>
