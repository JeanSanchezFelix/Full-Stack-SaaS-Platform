<script>
  let { customer = null, licenseNumber = "" } = $props();
  let showBookings = $state(false);
  let isEditing = $state(false);
  let isSaving = $state(false);
  let saveError = $state("");
  let profile = $state(customer);
  let form = $state({
    firstName: "",
    lastName: "",
    licenseNumber: "",
    phoneNumber: "",
    email: "",
    city: "",
    country: "",
  });

  const fullName = $derived(
    profile ? `${profile.firstName || ""} ${profile.lastName || ""}`.trim() : "",
  );
  const location = $derived([profile?.city, profile?.country].filter(Boolean).join(", "));
  const initials = $derived(
    fullName
      ? fullName
          .split(/\s+/)
          .slice(0, 2)
          .map((part) => part[0])
          .join("")
          .toUpperCase()
      : "U",
  );

  function formatDate(value) {
    if (!value) return "Pendiente";
    return new Intl.DateTimeFormat("es-PR", {
      dateStyle: "medium",
      timeStyle: "short",
    }).format(new Date(value));
  }

  function openEdit() {
    if (!profile) return;
    form = {
      firstName: profile.firstName || "",
      lastName: profile.lastName || "",
      licenseNumber: profile.licenseNumber || "",
      phoneNumber: profile.phoneNumber || "",
      email: profile.email || "",
      city: profile.city || "",
      country: profile.country || "",
    };
    saveError = "";
    isEditing = true;
  }

  function cancelEdit() {
    isEditing = false;
    saveError = "";
  }

  async function saveProfile() {
    if (!profile?.customerCode) return;
    isSaving = true;
    saveError = "";

    try {
      const response = await fetch("/Accounts/UpdateProfile", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          customerCode: profile.customerCode,
          licenseNumber: form.licenseNumber,
          firstName: form.firstName,
          lastName: form.lastName,
          phoneNumber: form.phoneNumber,
          email: form.email,
          city: form.city,
          country: form.country,
        }),
      });

      const payload = await response.json();
      if (!response.ok) {
        saveError = payload?.message || "No se pudo guardar.";
        return;
      }

      profile = { ...profile, ...payload };
      window.dispatchEvent(
        new CustomEvent("profile-license-updated", {
          detail: { licenseNumber: payload.licenseNumber || "" },
        }),
      );
      isEditing = false;
    } catch {
      saveError = "No se pudo guardar.";
    } finally {
      isSaving = false;
    }
  }

  function clearPersistedProfileState() {
    try {
      const keysToDelete = [];
      for (let i = 0; i < localStorage.length; i++) {
        const key = localStorage.key(i);
        if (!key) continue;
        if (key === "customer-intake-form" || key.startsWith("booking-draft:")) {
          keysToDelete.push(key);
        }
      }
      keysToDelete.forEach((key) => localStorage.removeItem(key));
      sessionStorage.removeItem("customer-intake-form");
    } catch {
      // Ignore storage access issues.
    }
  }

  function handleLogoffClick(event) {
    event.preventDefault();
    clearPersistedProfileState();
    window.location.href = "/Accounts/LogoffProfile";
  }
</script>

<aside class="mb-6 rounded-md border border-slate-200 bg-slate-50 px-4 py-4">
  {#if profile}
    <div class="flex gap-4 flex-row items-start justify-between">
      <div class="flex gap-4">
        <div class="flex h-12 w-12 shrink-0 items-center justify-center rounded-md bg-slate-950 text-sm font-bold text-white">
          {initials}
        </div>
        <div>
          <h2 class="flex items-center gap-2 text-lg font-bold tracking-normal text-slate-950">
            {fullName}
            <button
              type="button"
              title="Editar perfil"
              class="inline-flex h-6 w-6 items-center justify-center rounded-md text-slate-500 hover:bg-slate-100 hover:text-slate-700"
              onclick={openEdit}
            >
              <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <path d="M12 20h9" />
                <path d="M16.5 3.5a2.12 2.12 0 1 1 3 3L7 19l-4 1 1-4Z" />
              </svg>
            </button>
          </h2>
          <p class="mt-1 text-xs text-slate-600">{profile.customerCode}</p>
        </div>
      </div>

      <div class="flex flex-col items-end gap-2">
        <button
          type="button"
          class="w-fit rounded-full bg-white px-3 py-1 text-xs font-semibold text-slate-700 ring-1 ring-slate-200 transition hover:bg-slate-100"
          onclick={() => (showBookings = true)}
        >
          Ver Reservas ({profile.bookings?.length || 0})
        </button>
        <a
          href="/Accounts/LogoffProfile"
          class="w-fit rounded-full bg-white px-3 py-1 text-xs font-semibold text-slate-700 ring-1 ring-slate-200 transition hover:bg-slate-100"
          onclick={handleLogoffClick}
        >
          Cerrar perfil
        </a>
      </div>
    </div>

    {#if isEditing}
      <div class="mt-5 grid gap-3 border-t border-slate-200 pt-4 text-sm md:grid-cols-2">
        <input class="rounded-md border border-slate-300 px-3 py-2" bind:value={form.firstName} placeholder="Nombre" />
        <input class="rounded-md border border-slate-300 px-3 py-2" bind:value={form.lastName} placeholder="Apellido" />
        <input class="rounded-md border border-slate-300 px-3 py-2" bind:value={form.licenseNumber} placeholder="Licencia" />
        <input class="rounded-md border border-slate-300 px-3 py-2" bind:value={form.phoneNumber} placeholder="Telefono" />
        <input class="rounded-md border border-slate-300 px-3 py-2" bind:value={form.email} placeholder="Correo electronico" />
        <input class="rounded-md border border-slate-300 px-3 py-2" bind:value={form.city} placeholder="Ciudad" />
        <input class="rounded-md border border-slate-300 px-3 py-2" bind:value={form.country} placeholder="Pais" />
        {#if saveError}
          <p class="md:col-span-2 text-xs text-red-600">{saveError}</p>
        {/if}
        <div class="md:col-span-2 flex gap-2">
          <button type="button" class="rounded-md bg-slate-900 px-3 py-1.5 text-xs font-semibold text-white disabled:opacity-60" onclick={saveProfile} disabled={isSaving}>
            {isSaving ? "Guardando..." : "Guardar cambios"}
          </button>
          <button type="button" class="rounded-md border border-slate-300 px-3 py-1.5 text-xs font-semibold text-slate-700" onclick={cancelEdit}>
            Cancelar
          </button>
        </div>
      </div>
    {:else}
      <dl class="mt-5 grid gap-4 border-t border-slate-200 pt-4 text-sm md:grid-cols-2 lg:grid-cols-3">
        <div>
          <dt class="font-semibold text-slate-800">Licencia</dt>
          <dd class="mt-1 text-slate-600">{profile.licenseNumber || "No registrada"}</dd>
        </div>
        <div>
          <dt class="font-semibold text-slate-800">Telefono</dt>
          <dd class="mt-1 text-slate-600">{profile.phoneNumber || "No registrado"}</dd>
        </div>
        <div>
          <dt class="font-semibold text-slate-800">Correo electronico</dt>
          <dd class="mt-1 break-words text-slate-600">{profile.email || "No registrado"}</dd>
        </div>
        <div>
          <dt class="font-semibold text-slate-800">Ubicacion</dt>
          <dd class="mt-1 text-slate-600">{location || "No registrada"}</dd>
        </div>
        <div>
          <dt class="font-semibold text-slate-800">Cuenta registrada</dt>
          <dd class="mt-1 text-slate-600">{formatDate(profile.createdAt)}</dd>
        </div>
      </dl>
    {/if}
  {:else}
    <div>
      <p class="text-sm font-semibold text-slate-900">No hay cuenta cargada</p>
      <p class="mt-1 text-sm text-slate-600">
        {licenseNumber
          ? `No encontramos informacion para la licencia ${licenseNumber}.`
          : "Busca tu licencia en Reserva para cargar tu informacion antes de completar la reserva."}
      </p>
    </div>
  {/if}
</aside>

{#if showBookings}
  <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
    <div class="w-full max-w-3xl rounded-md border border-slate-200 bg-white p-4 shadow-sm">
      <div class="mb-4 flex items-center justify-between">
        <h3 class="text-base font-semibold text-slate-900">Historial de Reservas</h3>
        <button
          type="button"
          class="rounded-md px-2 py-1 text-sm text-slate-600 hover:bg-slate-100"
          onclick={() => (showBookings = false)}>Cerrar</button
        >
      </div>

      {#if profile?.bookings?.length > 0}
        <div class="max-h-80 overflow-auto rounded-md border border-slate-200">
          <table class="min-w-full divide-y divide-slate-200 text-sm">
            <thead class="bg-slate-50 text-left text-slate-600">
              <tr>
                <th class="px-3 py-2 font-semibold">Fecha</th>
                <th class="px-3 py-2 font-semibold">Inicio</th>
                <th class="px-3 py-2 font-semibold">Termina</th>
                <th class="px-3 py-2 font-semibold">Duracion</th>
                <th class="px-3 py-2 font-semibold">Scooters</th>
                <th class="px-3 py-2 font-semibold">E-bikes</th>
                <th class="px-3 py-2 font-semibold">Estado</th>
                <th class="px-3 py-2 font-semibold">Nota admin</th>
                <th class="px-3 py-2 font-semibold"></th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100">
              {#each profile.bookings as booking}
                <tr>
                  <td class="px-3 py-2 text-slate-700">{new Date(booking.requestedStart).toLocaleDateString("es-PR")}</td>
                  <td class="px-3 py-2 text-slate-700">{new Date(booking.requestedStart).toLocaleTimeString("es-PR", { hour: "2-digit", minute: "2-digit" })}</td>
                  <td class="px-3 py-2 text-slate-700">{booking.requestedEnd ? new Date(booking.requestedEnd).toLocaleTimeString("es-PR", { hour: "2-digit", minute: "2-digit" }) : "-"}</td>
                  <td class="px-3 py-2 text-slate-700">{booking.requestedEnd ? Math.round((new Date(booking.requestedEnd) - new Date(booking.requestedStart)) / 3600000) : "-"} h</td>
                  <td class="px-3 py-2 text-slate-700">{booking.scooterQuantity}</td>
                  <td class="px-3 py-2 text-slate-700">{booking.ebikeQuantity}</td>
                  <td class="px-3 py-2 font-medium text-slate-800">{booking.status}</td>
                  <td class="px-3 py-2 text-slate-600">{booking.adminNotes || "-"}</td>
                  <td class="px-3 py-2 text-right">
                    {#if booking.canDelete}
                      <a
                        class="rounded-md bg-slate-100 px-2 py-1 text-xs font-semibold text-slate-700 hover:bg-slate-200"
                        href={`/Bookings/DeleteOwn?id=${booking.id}&customerCode=${encodeURIComponent(profile.customerCode || "")}`}
                      >
                        Eliminar
                      </a>
                    {/if}
                    {#if booking.reconfirmRequested}
                      <div class="mt-1 flex gap-1">
                        <a
                          class="rounded-md bg-emerald-100 px-2 py-1 text-xs font-semibold text-emerald-700 hover:bg-emerald-200"
                          href={`/Bookings/RespondReconfirm?id=${booking.id}&accept=true&customerCode=${encodeURIComponent(profile.customerCode || "")}`}
                        >
                          Aceptar
                        </a>
                        <a
                          class="rounded-md bg-red-100 px-2 py-1 text-xs font-semibold text-red-700 hover:bg-red-200"
                          href={`/Bookings/RespondReconfirm?id=${booking.id}&accept=false&customerCode=${encodeURIComponent(profile.customerCode || "")}`}
                        >
                          Rechazar
                        </a>
                      </div>
                    {/if}
                  </td>
                </tr>
              {/each}
            </tbody>
          </table>
        </div>
      {:else}
        <p class="text-sm text-slate-600">No tienes reservas registradas.</p>
      {/if}
    </div>
  </div>
{/if}
