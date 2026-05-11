<script>
  let {
    label = "",
    href = "",
    type = "button",
    variant = "primary",
    block = false,
    extraClass = "",
  } = $props();

  const baseClass = variant === "link"
    ? "inline p-0 text-sm font-medium underline transition focus:outline-none"
    : "rounded-md px-5 py-2.5 text-sm font-semibold transition focus:outline-none focus:ring-2 focus:ring-offset-2";
  const variantClassMap = {
    primary: "bg-slate-950 text-white shadow-sm hover:bg-slate-800 focus:ring-slate-900",
    secondary: "text-slate-700 hover:bg-slate-100",
    subtle: "border border-slate-300 bg-white text-slate-700 hover:bg-slate-100",
    link: "text-slate-950 hover:text-slate-700",
  };
  const classes = `${baseClass} ${variantClassMap[variant] || variantClassMap.primary} ${block ? "w-full" : ""} ${extraClass}`.trim();
</script>

{#if href}
  <a href={href} class={classes}>
    {label}
  </a>
{:else}
  <button type={type} class={classes}>
    {label}
  </button>
{/if}
