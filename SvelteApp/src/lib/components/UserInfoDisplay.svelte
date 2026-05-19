<script>
  let { customer = null, licenseNumber = "" } = $props();
  let showBookings = $state(false);
  let isEditing = $state(false);
  let isSaving = $state(false);
  let saveError = $state("");
  let profile = $state(customer);
  const bookingRowsLimit = 6;
  let showAllBookings = $state(false);
  let showReviewModal = $state(false);
  let activeReviewBookingId = $state(null);
  let reviewRating = $state(5);
  let reviewComment = $state("");
  let reviewError = $state("");
  let isSubmittingReview = $state(false);
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

  function openReviewModal(booking) {
    if (booking?.reviewRating) return;
    activeReviewBookingId = booking?.id ?? null;
    reviewRating = booking?.reviewRating ?? 5;
    reviewComment = booking?.reviewComment ?? "";
    reviewError = "";
    showReviewModal = true;
  }

  function closeReviewModal() {
    showReviewModal = false;
    activeReviewBookingId = null;
    reviewError = "";
    isSubmittingReview = false;
  }

  async function submitReview() {
    if (!profile?.customerCode || !activeReviewBookingId) return;
    if (!reviewComment.trim()) {
      reviewError = "El comentario es requerido.";
      return;
    }

    isSubmittingReview = true;
    reviewError = "";

    try {
      const tokenInput = document.querySelector('#booking-form input[name="__RequestVerificationToken"]');
      const formData = new FormData();
      formData.append("id", String(activeReviewBookingId));
      formData.append("rating", String(reviewRating));
      formData.append("comment", reviewComment.trim());
      formData.append("customerCode", profile.customerCode || "");
      if (tokenInput?.value) {
        formData.append("__RequestVerificationToken", tokenInput.value);
      }

      const response = await fetch("/Bookings/SubmitReview", {
        method: "POST",
        body: formData,
      });
      const payload = await response.json();
      if (!response.ok) {
        reviewError = payload?.message || "No se pudo guardar la reseña.";
        return;
      }

      if (Array.isArray(profile?.bookings)) {
        profile = {
          ...profile,
          bookings: profile.bookings.map((booking) =>
            booking.id === activeReviewBookingId
              ? {
                  ...booking,
                  reviewRating: payload.reviewRating,
                  reviewComment: payload.reviewComment,
                  reviewCreatedAt: payload.reviewCreatedAt,
                }
              : booking,
          ),
        };
      }

      closeReviewModal();
    } catch {
      reviewError = "No se pudo guardar la reseña.";
    } finally {
      isSubmittingReview = false;
    }
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
          <button type="button" class="rounded-md bg-slate-900 px-3 py-1.5 text-xs font-semibold text-white disabled:opacity-60 hover:bg-slate-800" onclick={saveProfile} disabled={isSaving}>
            {isSaving ? "Guardando..." : "Guardar cambios"}
          </button>
          <button type="button" class="rounded-md border border-slate-300 px-3 py-1.5 text-xs font-semibold text-slate-700 hover:bg-slate-100" onclick={cancelEdit}>
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
          <dt class="font-semibold text-slate-800">Teléfono</dt>
          <dd class="mt-1 text-slate-600">{profile.phoneNumber || "No registrado"}</dd>
        </div>
        <div>
          <dt class="font-semibold text-slate-800">Correo electrónico</dt>
          <dd class="mt-1 break-words text-slate-600">{profile.email || "No registrado"}</dd>
        </div>
        <div>
          <dt class="font-semibold text-slate-800">Ubicación</dt>
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
          ? `No encontramos información para la licencia ${licenseNumber}.`
          : "Busca tu licencia en Reserva para cargar tu información antes de completar la reserva."}
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
          onclick={() => {
            showBookings = false;
            showAllBookings = false;
          }}>Cerrar</button
        >
      </div>

      {#if profile?.bookings?.length > 0}
        <div class="max-h-80 overflow-auto rounded-md border border-slate-200">
          <table class="min-w-full divide-y divide-slate-200 text-sm">
            <thead class="bg-slate-50 text-center text-slate-700 ">
              <tr>
                <th class="px-3 py-2 font-semibold">Fecha</th>
                <th class="px-3 py-2 font-semibold">Comienza</th>
                <th class="px-3 py-2 font-semibold">Concluye</th>
                <th class="px-3 py-2 font-semibold">Duración</th>
                <th class="px-3 py-2 font-semibold">Scooters</th>
                <th class="min-w-[70px] px-3 py-2 font-semibold">E-bikes</th>
                <th class="px-3 py-2 font-semibold">Total estimado</th>
                <th class="px-3 py-2 font-semibold"></th>
                <!-- <th class="px-3 py-2 font-semibold">Estado</th> -->
                <!-- <th class="px-3 py-2 font-semibold">Nota admin</th> -->
                <!-- <th class="px-3 py-2 font-semibold"></th> -->
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100">
              {#each profile.bookings as booking, index}
                {#if showAllBookings || index < bookingRowsLimit}
                <tr class="odd:bg-white even:bg-slate-50 text-center">
                  <td class="px-3 py-2 text-slate-700">{new Date(booking.requestedStart).toLocaleDateString("es-PR")}</td>
                  <td class="min-w-[100px] px-3 py-2 text-slate-700 whitespace-nowrap">{new Date(booking.requestedStart).toLocaleTimeString("es-PR", { hour: "2-digit", minute: "2-digit"})}</td>                  
                  <td class="min-w-[100px] px-3 py-2 text-slate-700">{booking.requestedEnd ? new Date(booking.requestedEnd).toLocaleTimeString("es-PR", { hour: "2-digit", minute: "2-digit" }) : "-"}</td>
                  <td class="px-3 py-2 text-slate-700">{booking.requestedEnd ? Math.round((((new Date(booking.requestedEnd) - new Date(booking.requestedStart)) / 3600000) * 10)) / 10 : "-"} h</td>
                  <td class="px-3 py-2 text-slate-700">{booking.scooterQuantity}</td>
                  <td class="px-3 py-2 text-slate-700">{booking.ebikeQuantity}</td>
                  <td class="px-3 py-2 text-slate-700">${booking.estimatedTotal.toFixed(2)}</td>
                  <td class="px-3 py-2 text-center">
                    <button
                      type="button"
                      class={!booking.reviewRating
                        ? "inline-flex items-center justify-center rounded-md border border-slate-300 bg-white p-1.5 text-slate-700 transition hover:bg-blue-50 hover:text-[#267DA1]"
                        : "inline-flex items-center justify-center rounded-md border border-slate-200 bg-slate-100 p-1.5 text-slate-400 cursor-not-allowed"}
                      title={!booking.reviewRating
                        ? "Escribir reseña"
                        : booking.reviewRating
                        ? "Reseña enviada"
                        : "No disponible"}
                      onclick={() => openReviewModal(booking)}
                      disabled={!!booking.reviewRating}
                    >
                      {#if booking.reviewRating}
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                          <path d="M20 6 9 17l-5-5"></path>
                        </svg>
                      {:else}
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                          <path d="M21 15a4 4 0 0 1-4 4H8l-5 3V7a4 4 0 0 1 4-4h10a4 4 0 0 1 4 4z"></path>
                        </svg>
                      {/if}
                    </button>
                    <!-- {#if booking.reviewRating}
                      <p class="mt-1 text-[11px] font-semibold text-amber-600">{booking.reviewRating}/5</p>
                      {#if booking.reviewComment}
                        <p class="mt-0.5 max-w-[160px] truncate text-[11px] text-slate-500" title={booking.reviewComment}>{booking.reviewComment}</p>
                      {/if}
                    {/if} -->
                  </td>
                  <!-- <td class="px-3 py-2 font-medium text-slate-800">{booking.status}</td> -->
                  <!-- <td class="px-3 py-2 text-slate-600">{booking.adminNotes || "-"}</td> -->
                  <!-- <td class="px-3 py-2 text-right">
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
                  </td> -->
                </tr>
                {/if}
              {/each}
            </tbody>
          </table>
        </div>
        {#if profile.bookings.length > bookingRowsLimit}
          <div class="mt-3 flex justify-start">
            <button
              type="button"
              class="rounded-md border border-slate-300 px-3 py-1.5 text-xs font-semibold text-slate-700 hover:bg-slate-100"
              onclick={() => (showAllBookings = !showAllBookings)}
            >
              {showAllBookings ? "Ver menos" : "Ver más"}
            </button>
          </div>
        {/if}
      {:else}
        <p class="text-sm text-slate-600 text-center">No tienes reservas registradas.</p>
      {/if}
    </div>
  </div>
{/if}

{#if showReviewModal}
  <div class="fixed inset-0 z-[60] flex items-center justify-center bg-black/40 p-4">
    <div class="w-full max-w-lg rounded-md border border-t-4 border-[#267DA1] bg-white p-5 shadow-sm">
      <div class="mb-4 flex items-center justify-between">
        <h3 class="text-base font-semibold text-slate-900">Observaciones</h3>
        <button type="button" class="rounded-md px-2 py-1 text-sm text-slate-600 hover:bg-slate-100" onclick={closeReviewModal}>Cerrar</button>
      </div>
      <p class="text-sm text-slate-600">Campo para registrar notas administrativas o comentarios internos relevantes a la gestión de la reserva.</p>

      <!-- <div class="mt-4">
        <label
          for="reviewRating"
          class="block text-sm font-semibold text-slate-800"
        >
          Calificación (1-10)
        </label>

        <select
          id="reviewRating"
          class="mt-2 w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm"
          bind:value={reviewRating}
        >
          <option value={1}>1 - Muy mala</option>
          <option value={2}>2 - Mala</option>
          <option value={3}>3 - Regular</option>
          <option value={4}>4 - Aceptable</option>
          <option value={5}>5 - Buena</option>
          <option value={6}>6 - Muy buena</option>
          <option value={7}>7 - Destacable</option>
          <option value={8}>8 - Excelente</option>
          <option value={9}>9 - Casi perfecta</option>
          <option value={10}>10 - Perfecta</option>
        </select>
      </div> -->

      <div class="mt-4">
        <label
          for="reviewComment"
          class="block text-sm font-semibold text-slate-800"
        >
          Comentario
        </label>

        <textarea
          id="reviewComment"
          class="mt-2 w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm"
          rows="4"
          maxlength="1000"
          bind:value={reviewComment}
          placeholder="Agregue aquí su observación..."
        ></textarea>
      </div>

      {#if reviewError}
        <p class="mt-2 text-sm text-red-600">{reviewError}</p>
      {/if}

      <div class="mt-4 flex items-center justify-end gap-2">
        <button type="button" class="rounded-md border border-slate-300 px-3 py-1.5 text-xs font-semibold text-slate-700 hover:bg-slate-100" onclick={closeReviewModal}>
          Cancelar
        </button>
        <button
          type="button"
          class="rounded-md bg-slate-900 px-3 py-1.5 text-xs font-semibold text-white hover:bg-slate-800 disabled:opacity-60"
          onclick={submitReview}
          disabled={isSubmittingReview}
        >
          {isSubmittingReview ? "Guardando..." : "Guardar reseña"}
        </button>
      </div>
    </div>
  </div>
{/if}

