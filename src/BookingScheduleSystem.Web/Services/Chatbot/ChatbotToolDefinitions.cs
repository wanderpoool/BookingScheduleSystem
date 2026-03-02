using System.Text.Json;

namespace BookingScheduleSystem.Web.Services.Chatbot;

public static class ChatbotToolDefinitions
{
    public static JsonElement GetToolDefinitions()
    {
        var tools = """
        [
            {
                "name": "check_availability",
                "description": "Check available appointment slots for the business. Returns providers (with provider_id) and their time blocks per day. Each day includes 'available_slots' (existing bookable slots with schedule_id) and 'free_time' (open working hours where new appointments can be created). Use this when the user asks about availability or wants to see what times are free.",
                "input_schema": {
                    "type": "object",
                    "properties": {
                        "start_date": {
                            "type": "string",
                            "description": "Start date in YYYY-MM-DD format. Defaults to today if not specified."
                        },
                        "end_date": {
                            "type": "string",
                            "description": "End date in YYYY-MM-DD format. Defaults to 7 days from start if not specified."
                        }
                    },
                    "required": []
                }
            },
            {
                "name": "send_otp",
                "description": "Send a one-time password to the user's email or phone for verification. Must be called before register_user. Ask the user for their preferred contact method and details first.",
                "input_schema": {
                    "type": "object",
                    "properties": {
                        "contact_method": {
                            "type": "string",
                            "enum": ["email", "phone"],
                            "description": "How to send the OTP — via email or phone SMS."
                        },
                        "email": {
                            "type": "string",
                            "description": "The user's email address. Required if contact_method is 'email'."
                        },
                        "phone_number": {
                            "type": "string",
                            "description": "The user's phone number. Required if contact_method is 'phone'."
                        }
                    },
                    "required": ["contact_method"]
                }
            },
            {
                "name": "verify_otp",
                "description": "Verify the OTP code that the user received. Must be called after send_otp and before register_user.",
                "input_schema": {
                    "type": "object",
                    "properties": {
                        "otp_code": {
                            "type": "string",
                            "description": "The 6-digit OTP code entered by the user."
                        }
                    },
                    "required": ["otp_code"]
                }
            },
            {
                "name": "register_user",
                "description": "Register a new user account and authenticate them. Must be called after verify_otp succeeds. Collects the user's name and auto-generates a password.",
                "input_schema": {
                    "type": "object",
                    "properties": {
                        "first_name": {
                            "type": "string",
                            "description": "The user's first name."
                        },
                        "last_name": {
                            "type": "string",
                            "description": "The user's last name."
                        }
                    },
                    "required": ["first_name", "last_name"]
                }
            },
            {
                "name": "create_booking",
                "description": "Book an existing schedule slot. Use this ONLY when check_availability returned 'available_slots' with a schedule_id. Must be called after the user is registered. IMPORTANT: Always ask the user for special instructions or notes BEFORE calling this tool.",
                "input_schema": {
                    "type": "object",
                    "properties": {
                        "schedule_id": {
                            "type": "string",
                            "description": "The UUID of the schedule slot to book, from check_availability available_slots."
                        },
                        "notes": {
                            "type": "string",
                            "description": "Customer's special instructions, concerns, or notes for the appointment. Ask the user before calling this tool."
                        }
                    },
                    "required": ["schedule_id"]
                }
            },
            {
                "name": "create_and_book",
                "description": "Create a new appointment slot during a provider's free time and book it in one step. Use this when check_availability shows 'free_time' windows but no 'available_slots'. The time must fall within the provider's free_time range. Must be called after the user is registered. Default appointment duration is 1 hour unless the user specifies otherwise. IMPORTANT: Always ask the user for special instructions or notes BEFORE calling this tool.",
                "input_schema": {
                    "type": "object",
                    "properties": {
                        "provider_id": {
                            "type": "string",
                            "description": "The UUID of the provider, from check_availability results."
                        },
                        "date": {
                            "type": "string",
                            "description": "Appointment date in YYYY-MM-DD format."
                        },
                        "start_time": {
                            "type": "string",
                            "description": "Start time in HH:mm 24-hour format (e.g., '10:00')."
                        },
                        "end_time": {
                            "type": "string",
                            "description": "End time in HH:mm 24-hour format (e.g., '11:00')."
                        },
                        "notes": {
                            "type": "string",
                            "description": "Customer's special instructions, concerns, or notes for the appointment. Ask the user before calling this tool."
                        }
                    },
                    "required": ["provider_id", "date", "start_time", "end_time"]
                }
            },
            {
                "name": "list_my_bookings",
                "description": "List the authenticated user's bookings. Requires the user to be registered/signed in first. Returns booking details including status, date/time, and whether the booking is in the future (can be cancelled or rescheduled).",
                "input_schema": {
                    "type": "object",
                    "properties": {
                        "status_filter": {
                            "type": "string",
                            "enum": ["all", "pending", "confirmed", "cancelled"],
                            "description": "Filter bookings by status. Defaults to 'all' if not specified."
                        }
                    },
                    "required": []
                }
            },
            {
                "name": "cancel_booking",
                "description": "Cancel a specific booking by its ID. Requires the user to be registered/signed in. Always confirm with the user before calling this tool.",
                "input_schema": {
                    "type": "object",
                    "properties": {
                        "booking_id": {
                            "type": "string",
                            "description": "The UUID of the booking to cancel."
                        },
                        "reason": {
                            "type": "string",
                            "description": "Optional reason for cancellation."
                        }
                    },
                    "required": ["booking_id"]
                }
            }
        ]
        """;

        return JsonSerializer.Deserialize<JsonElement>(tools);
    }
}
