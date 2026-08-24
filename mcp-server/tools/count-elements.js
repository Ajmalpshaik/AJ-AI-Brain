import { filterFields, buildElementsClause, runGenerated } from "../shared/element-filter.js";
import { defineTool } from "../shared/register.js";

export function register(server) {
  defineTool(server,
    "count_elements",
    "Bare count of matching elements for ANY category (not limited to model_summary's fixed list), " +
      "with an optional numeric-parameter filter. Use for plain 'how many' questions.",
    filterFields,
    async (filter) => {
      const script = [buildElementsClause(filter), `sb.AppendLine("Count: " + elements.Count);`, `return sb.ToString();`].join("\n");
      return runGenerated(script);
    }
  );
}
