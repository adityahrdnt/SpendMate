<script setup lang="ts">
import { computed, onMounted, watch } from "vue";
import { useFamilies } from "../composables/useFamilies";
import type { FamilyMember, FamilyRole } from "../types/family";

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
} = useFamilies();

const isOwner = computed(() => props.familyRole === "Owner");

const ownerCount = computed(
  () => familyMembers.value.filter((member) => member.role === "Owner").length,
);

const canLeaveFamily = computed(
  () => isOwner.value && ownerCount.value > 1,
);

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
