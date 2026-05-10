<script>
  let {
    fieldName = '',
    label = '',
    value = '',
    error = '',
    type = 'text',
    required = false,
    hint = '',
    multiline = false,
    rows = 3,
    wrapperClass = '',
  } = $props();
</script>

<div class={wrapperClass}>
  <label for={fieldName} class="block text-sm font-medium text-slate-800">
    {label}
    {#if required}
      <span class="ml-1 text-red-600" aria-hidden="true">*</span>
      <span class="sr-only">required</span>
    {/if}
  </label>
  {#if multiline}
    <textarea
      id={fieldName}
      name={fieldName}
      rows={rows}
      class="mt-2 w-full rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-900 focus:outline-none focus:ring-1 focus:ring-slate-900"
    >{value || ''}</textarea>
  {:else}
    <input
      id={fieldName}
      name={fieldName}
      type={type}
      class="mt-2 w-full rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-900 focus:outline-none focus:ring-1 focus:ring-slate-900"
      value={value || ''}
    />
  {/if}
  {#if hint}
    <p class="mt-2 text-xs text-slate-500">{hint}</p>
  {/if}
  <span class="mt-1 block text-sm text-red-600">{error}</span>
</div>
