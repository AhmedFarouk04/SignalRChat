// تنقية وتنسيق رسائل النظام
export function formatSystemMessage(text) {
    const lower = text.toLowerCase();

    if (lower.includes("created the group")) {
        return `Group created`;
    } else if (lower.includes("was added by")) {
        const parts = text.split(" was added by ");
        return `${parts[0]} joined`;
    } else if (lower.includes("was removed by")) {
        const parts = text.split(" was removed by ");
        return `${parts[0]} removed`;
    } else if (lower.includes("left")) {
        const parts = text.split(" left");
        return `${parts[0]} left the group`;
    }

    return text;
}