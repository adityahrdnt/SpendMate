
<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { useFamilies } from "../composables/useFamilies";
import type {
  FamilyInvitationType,
  FamilyMember,
  FamilyRole,
} from "../types/family";

const props = defineProps<{
  familyId: string;
  currentUserId: string;
  familyRole: FamilyRole;
}>();

const emit = defineEmits<{
  "family-left": [];
}>();

const {
  familyMembers,
  isLoadingMembers,
  isUpdatingMemberRole,
  isRemovingMember,
  memberErrorMessage,
  loadFamilyMembers,
  updateMemberRole,
  removeMember,
  isAddingMember,
  isCreatingInvitation,
  invitationErrorMessage,
  invitationSuccessMessage,
  addMemberByPhone,
  createInvitation,
} = useFamilies();

const isOwner = computed(() => props.familyRole === "Owner");

const ownerCount = computed(
  () => familyMembers.value.filter((member) => member.role === "Owner").length,
);

const canLeaveFamily = computed(
  () => isOwner.value && ownerCount.value > 1,
);

const invitationType = ref<FamilyInvitationType>("Email");
const invitationEmail = ref("");
const invitationPhone = ref("");
const memberPhone = ref("");

const invitationLink = ref("");
const invitationLinkCopied = ref(false);
const memberSuccessMessage = ref("");

const canSubmitInvitation = computed(() => {
  if (invitationType.value === "Email") {
    return invitationEmail.value.trim().length > 0;
  }

  return invitationPhone.value.trim().length > 0;
});

function isCurrentUser(member: FamilyMember): boolean {
  return member.userId === props.currentUserId;
}

async function handleRoleChange(
  member: FamilyMember,
  event: Event,
): Promise<void> {
  const target = event.target as HTMLSelectElement;
  const newRole = target.value as FamilyRole;

  if (newRole === member.role || isCurrentUser(member)) {
    return;
  }

  const success = await updateMemberRole(
    props.familyId,
    member.userId,
    { role: newRole },
  );

  if (success) {
    await loadFamilyMembers(props.familyId);
  } else {
    target.value = member.role;
  }
}

async function handleRemoveMember(member: FamilyMember): Promise<void> {
  const isSelf = isCurrentUser(member);
  const action = isSelf ? "leave this family" : "remove this member";

  if (!window.confirm(`Are you sure you want to ${action}?`)) {
    return;
  }

  const success = await removeMember(props.familyId, member.userId);

  if (!success) {
    return;
  }

  if (isSelf) {
    emit("family-left");
    return;
  }

  await loadFamilyMembers(props.familyId);
}

async function handleCreateInvitation(): Promise<void> {
  invitationLink.value = "";
  invitationLinkCopied.value = false;

  const request =
    invitationType.value === "Email"
      ? {
          invitationType: "Email" as const,
          email: invitationEmail.value.trim(),
        }
      : {
          invitationType: "Phone" as const,
          phoneNumber: invitationPhone.value.trim(),
        };

  const result = await createInvitation(props.familyId, request);

  if (result) {
    invitationLink.value = result.invitationLink;
  }
}

async function handleAddMember(): Promise<void> {
  memberSuccessMessage.value = "";

  const success = await addMemberByPhone(props.familyId, {
    phoneNumber: memberPhone.value.trim(),
  });

  if (success) {
    memberPhone.value = "";
    memberSuccessMessage.value = "Family member added successfully.";
  }
}

async function handleCopyInvitationLink(): Promise<void> {
  if (!invitationLink.value) {
    return;
  }

  try {
    await navigator.clipboard.writeText(invitationLink.value);
    invitationLinkCopied.value = true;
  } catch {
    invitationLinkCopied.value = false;
  }
}

watch(invitationType, () => {
  invitationLink.value = "";
  invitationLinkCopied.value = false;
});

onMounted(() => {
  if (props.familyId) {
    void loadFamilyMembers(props.familyId);
  }
});

watch(
  () => props.familyId,
  (familyId) => {
    if (familyId) {
      void loadFamilyMembers(familyId);
    }
  },
);
</script>

<template>
  <section class="family-management-page">
    <section class="page-heading">
      <div>
        <div class="greeting">WORKSPACE SETTINGS</div>
        <h1>Family Management</h1>
        <p class="subtitle">
          Manage your family members and their access.
        </p>
      </div>
    </section>

    <!-- Owner-only family management actions -->
    <template v-if="isOwner">
      <!-- Invite Member -->
      <section class="panel family-action-panel">
        <div class="panel-heading">
          <div>
            <h2>Invite Member</h2>
            <p class="panel-subtitle">
              Invite someone to join your family.
            </p>
          </div>
        </div>

        <form class="family-form" @submit.prevent="handleCreateInvitation">
          <div class="family-form-group">
            <label for="invitation-type">Invitation method</label>
            <select
              id="invitation-type"
              v-model="invitationType"
              :disabled="isCreatingInvitation"
            >
              <option value="Email">Email</option>
              <option value="Phone">Phone</option>
            </select>
          </div>

          <div
            v-if="invitationType === 'Email'"
            class="family-form-group"
          >
            <label for="invitation-email">Email address</label>
            <input
              id="invitation-email"
              v-model="invitationEmail"
              type="email"
              placeholder="name@example.com"
              autocomplete="email"
              required
              :disabled="isCreatingInvitation"
            />
          </div>

          <div v-else class="family-form-group">
            <label for="invitation-phone">Phone number</label>
            <input
              id="invitation-phone"
              v-model="invitationPhone"
              type="tel"
              placeholder="+6281234567890"
              autocomplete="tel"
              required
              :disabled="isCreatingInvitation"
            />
            <small>Use international format, for example +6281234567890.</small>
          </div>

          <p
            v-if="invitationErrorMessage"
            class="family-form-message error"
            role="alert"
          >
            {{ invitationErrorMessage }}
          </p>

          <p
            v-if="invitationSuccessMessage"
            class="family-form-message success"
            role="status"
          >
            {{ invitationSuccessMessage }}
          </p>

          <button
            type="submit"
            class="primary-button"
            :disabled="!canSubmitInvitation || isCreatingInvitation"
          >
            {{ isCreatingInvitation ? "Creating..." : "Create Invitation" }}
          </button>
        </form>

        <div v-if="invitationLink" class="invitation-link-section">
          <label for="generated-invitation-link">Invitation link</label>
          <div class="invitation-link-row">
            <input
              id="generated-invitation-link"
              :value="invitationLink"
              type="text"
              readonly
            />
            <button
              type="button"
              class="secondary-button"
              @click="handleCopyInvitationLink"
            >
              {{ invitationLinkCopied ? "Copied" : "Copy Link" }}
            </button>
          </div>
          <p class="family-form-hint">
            Share this link with the invited person so they can join your family.
          </p>
        </div>
      </section>

      <!-- Add Existing Member -->
      <section class="panel family-action-panel">
        <div class="panel-heading">
          <div>
            <h2>Add Existing Member</h2>
            <p class="panel-subtitle">
              Add a registered user using their verified phone number.
            </p>
          </div>
        </div>

        <form class="family-form" @submit.prevent="handleAddMember">
          <div class="family-form-group">
            <label for="existing-member-phone">Verified phone number</label>
            <input
              id="existing-member-phone"
              v-model="memberPhone"
              type="tel"
              placeholder="+6281234567890"
              autocomplete="off"
              required
              :disabled="isAddingMember"
            />
            <small>
              The user must already have a SpendMate account and a verified phone number.
            </small>
          </div>

          <p
            v-if="memberErrorMessage"
            class="family-form-message error"
            role="alert"
          >
            {{ memberErrorMessage }}
          </p>

          <p
            v-if="memberSuccessMessage"
            class="family-form-message success"
            role="status"
          >
            {{ memberSuccessMessage }}
          </p>

          <button
            type="submit"
            class="primary-button"
            :disabled="!memberPhone.trim() || isAddingMember"
          >
            {{ isAddingMember ? "Adding..." : "Add Member" }}
          </button>
        </form>
      </section>
    </template>

    <!-- Family Members -->
    <section class="panel family-members-panel">
      <div class="panel-heading">
        <div>
          <h2>Family members</h2>
          <p class="panel-subtitle">
            {{ familyMembers.length }} member(s) in this family
          </p>
        </div>
      </div>

      <div v-if="isLoadingMembers" class="empty-state" role="status">
        Loading family members...
      </div>

      <div v-else-if="memberErrorMessage" class="empty-state" role="alert">
        <p>{{ memberErrorMessage }}</p>
        <button
          type="button"
          class="secondary-button"
          @click="loadFamilyMembers(familyId)"
        >
          Try again
        </button>
      </div>

      <div v-else-if="familyMembers.length === 0" class="empty-state">
        No family members found.
      </div>

      <div v-else class="family-members-list">
        <article
          v-for="member in familyMembers"
          :key="member.userId"
          class="family-member-row"
        >
          <div class="family-member-info">
            <div class="profile-avatar">
              {{ member.displayName.charAt(0).toUpperCase() }}
            </div>

            <div class="family-member-copy">
              <strong>
                {{ member.displayName }}
                <span v-if="isCurrentUser(member)" class="you-label">
                  You
                </span>
              </strong>
              <span>{{ member.email }}</span>
              <small>
                Joined {{ new Date(member.joinedAtUtc).toLocaleDateString() }}
              </small>
            </div>
          </div>

          <div class="family-member-actions">
            <select
              v-if="isOwner && !isCurrentUser(member)"
              :value="member.role"
              aria-label="Member role"
              :disabled="isUpdatingMemberRole || isRemovingMember"
              @change="handleRoleChange(member, $event)"
            >
              <option value="Member">Member</option>
              <option value="Owner">Owner</option>
            </select>

            <span v-else class="category-pill">
              {{ member.role }}
            </span>

            <button
              v-if="isOwner && (!isCurrentUser(member) || canLeaveFamily)"
              type="button"
              class="secondary-button"
              :disabled="isRemovingMember || isUpdatingMemberRole"
              @click="handleRemoveMember(member)"
            >
              {{ isCurrentUser(member) ? "Leave Family" : "Remove" }}
            </button>
          </div>
        </article>
      </div>

      <p
        v-if="isOwner && !canLeaveFamily && ownerCount === 1"
        class="family-management-note"
      >
        You are the only Owner. Assign another Owner before leaving this family.
      </p>
    </section>
  </section>
</template>
