// ============================================================
// TravelMap — Interactive world map with country visit tracking
// ============================================================

(function () {
  // Theme: apply immediately to avoid flash
  var theme = localStorage.getItem("theme") || "dark";
  document.documentElement.setAttribute("data-theme", theme);
})();

document.addEventListener("DOMContentLoaded", function () {
  var config = window.TravelMapConfig || {};
  var visits = {}; // countryCode -> CountryVisit
  var geoLayer = null;

  // ---- Color scheme ----
  var COLORS = {
    1: "#28a745", // Mainland - green
    2: "#17a2b8", // Islands - blue
    3: "#ffc107", // Both - gold
  };

  var VISIT_LABELS = {
    1: "Mainland",
    2: "Islands",
    3: "Both",
  };

  // ---- Map initialization ----
  var map = L.map("map", {
    center: [30, 10],
    zoom: 3,
    minZoom: 2,
    maxZoom: 8,
    zoomControl: true,
    worldCopyJump: true,
  });

  // Tile layer — use CartoDB dark/light based on theme
  var darkTiles =
    "https://{s}.basemaps.cartocdn.com/dark_nolabels/{z}/{x}/{y}{r}.png";
  var lightTiles =
    "https://{s}.basemaps.cartocdn.com/light_nolabels/{z}/{x}/{y}{r}.png";
  var currentTheme = localStorage.getItem("theme") || "dark";
  var tileLayer = L.tileLayer(
    currentTheme === "light" ? lightTiles : darkTiles,
    {
      attribution:
        '&copy; <a href="https://www.openstreetmap.org/copyright">OSM</a> &copy; <a href="https://carto.com/">CARTO</a>',
      subdomains: "abcd",
      maxZoom: 19,
    }
  ).addTo(map);

  // ---- Theme toggle ----
  var themeToggle = document.getElementById("theme-toggle");
  var themeIcon = document.getElementById("theme-icon");

  function applyTheme(t) {
    document.documentElement.setAttribute("data-theme", t);
    themeIcon.className = t === "light" ? "bi bi-sun" : "bi bi-moon-stars";
    localStorage.setItem("theme", t);

    // Swap tile layer
    var url = t === "light" ? lightTiles : darkTiles;
    tileLayer.setUrl(url);

    // Re-style GeoJSON borders
    if (geoLayer) {
      geoLayer.eachLayer(function (layer) {
        var code = layer.feature.properties.ISO_A3 || layer.feature.properties.ADM0_A3;
        var visit = visits[code];
        layer.setStyle(getStyle(layer.feature, !!visit, t));
      });
    }
  }

  // Set initial icon
  themeIcon.className =
    currentTheme === "light" ? "bi bi-sun" : "bi bi-moon-stars";

  themeToggle.addEventListener("click", function () {
    var current = localStorage.getItem("theme") || "dark";
    applyTheme(current === "dark" ? "light" : "dark");
  });

  // ---- Country styling ----
  function getStyle(feature, isVisited, themeOverride) {
    var t = themeOverride || localStorage.getItem("theme") || "dark";
    var code = feature.properties.ISO_A3 || feature.properties.ADM0_A3;
    var visit = visits[code];

    if (visit) {
      return {
        fillColor: COLORS[visit.visitType] || COLORS[1],
        fillOpacity: 0.55,
        color: COLORS[visit.visitType] || COLORS[1],
        weight: 1.5,
        opacity: 0.8,
      };
    }

    return {
      fillColor: "transparent",
      fillOpacity: 0.01,
      color: t === "light" ? "#9ca3af" : "#30363d",
      weight: 0.8,
      opacity: 0.6,
    };
  }

  // ---- Hover + click handlers ----
  function onEachFeature(feature, layer) {
    var props = feature.properties;
    var name = props.NAME || props.ADMIN || "Unknown";
    var code = props.ISO_A3 || props.ADM0_A3;

    // Tooltip on hover
    layer.bindTooltip(name, {
      sticky: true,
      className: "country-tooltip",
    });

    // Hover highlight
    layer.on("mouseover", function () {
      if (!layer._isPopupOpen) {
        layer.setStyle({ weight: 2.5, opacity: 1 });
        layer.bringToFront();
      }
    });

    layer.on("mouseout", function () {
      if (!layer._isPopupOpen) {
        var visit = visits[code];
        layer.setStyle(getStyle(feature, !!visit));
      }
    });

    // Click: show popup (only when authenticated)
    layer.on("click", function () {
      if (!config.isAuthenticated) return;
      layer._isPopupOpen = true;
      showPopup(layer, code, name);
    });

    layer.on("popupclose", function () {
      layer._isPopupOpen = false;
      var visit = visits[code];
      layer.setStyle(getStyle(feature, !!visit));
    });
  }

  // ---- Popup (edit form) ----
  function showPopup(layer, code, name) {
    var visit = visits[code] || null;
    var selectedType = visit ? visit.visitType : 1;
    var firstVisited = visit && visit.firstVisited
      ? visit.firstVisited.substring(0, 10)
      : "";
    var lastVisited = visit && visit.lastVisited
      ? visit.lastVisited.substring(0, 10)
      : "";
    var notes = visit ? visit.notes || "" : "";

    var html =
      '<div class="popup-form">' +
      '<div class="popup-title">' + escapeHtml(name) + "</div>" +
      "<label>Visit type</label>" +
      '<div class="radio-group">' +
      '<div class="radio-btn' + (selectedType === 1 ? " active" : "") + '" data-value="1">Mainland</div>' +
      '<div class="radio-btn' + (selectedType === 2 ? " active" : "") + '" data-value="2">Islands</div>' +
      '<div class="radio-btn' + (selectedType === 3 ? " active" : "") + '" data-value="3">Both</div>' +
      "</div>" +
      "<label>First visited</label>" +
      '<input type="date" class="popup-first-visited" value="' + firstVisited + '">' +
      "<label>Last visited</label>" +
      '<input type="date" class="popup-last-visited" value="' + lastVisited + '">' +
      "<label>Notes</label>" +
      '<textarea class="popup-notes" placeholder="Optional notes...">' + escapeHtml(notes) + "</textarea>" +
      '<div class="popup-actions">' +
      '<button class="btn-save" data-code="' + code + '" data-name="' + escapeHtml(name) + '">Save</button>' +
      (visit
        ? '<button class="btn-remove" data-code="' + code + '">Remove</button>'
        : "") +
      "</div>" +
      "</div>";

    layer
      .bindPopup(html, {
        maxWidth: 280,
        minWidth: 240,
        closeButton: true,
      })
      .openPopup();

    // Wire up radio buttons
    setTimeout(function () {
      var popup = document.querySelector(".popup-form");
      if (!popup) return;

      popup.querySelectorAll(".radio-btn").forEach(function (btn) {
        btn.addEventListener("click", function () {
          popup.querySelectorAll(".radio-btn").forEach(function (b) {
            b.classList.remove("active");
          });
          btn.classList.add("active");
        });
      });

      // Save button
      var saveBtn = popup.querySelector(".btn-save");
      if (saveBtn) {
        saveBtn.addEventListener("click", function () {
          var activeType = popup.querySelector(".radio-btn.active");
          var visitType = activeType
            ? parseInt(activeType.getAttribute("data-value"))
            : 1;

          var visitData = {
            countryCode: code,
            countryName: name,
            visitType: visitType,
            firstVisited:
              popup.querySelector(".popup-first-visited").value || null,
            lastVisited:
              popup.querySelector(".popup-last-visited").value || null,
            notes: popup.querySelector(".popup-notes").value || null,
          };

          saveVisit(visitData, layer);
        });
      }

      // Remove button
      var removeBtn = popup.querySelector(".btn-remove");
      if (removeBtn) {
        removeBtn.addEventListener("click", function () {
          deleteVisit(code, layer);
        });
      }
    }, 50);
  }

  // ---- API calls ----
  function saveVisit(visitData, layer) {
    fetch("/api/visits", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(visitData),
    })
      .then(function (res) {
        if (!res.ok) throw new Error("Save failed");
        return res.json();
      })
      .then(function (saved) {
        visits[saved.countryCode] = saved;
        layer.setStyle(getStyle(layer.feature, true));
        layer.closePopup();
        updateStats();
      })
      .catch(function (err) {
        console.error("Save error:", err);
      });
  }

  function deleteVisit(code, layer) {
    fetch("/api/visits/" + encodeURIComponent(code), {
      method: "DELETE",
    })
      .then(function (res) {
        if (!res.ok) throw new Error("Delete failed");
        delete visits[code];
        layer.setStyle(getStyle(layer.feature, false));
        layer.closePopup();
        updateStats();
      })
      .catch(function (err) {
        console.error("Delete error:", err);
      });
  }

  function loadVisits() {
    if (!config.isAuthenticated) return Promise.resolve();

    return fetch("/api/visits")
      .then(function (res) {
        if (!res.ok) throw new Error("Load failed");
        return res.json();
      })
      .then(function (data) {
        visits = {};
        data.forEach(function (v) {
          visits[v.countryCode] = v;
        });
        updateStats();
        // Re-style all countries
        if (geoLayer) {
          geoLayer.eachLayer(function (layer) {
            var code =
              layer.feature.properties.ISO_A3 ||
              layer.feature.properties.ADM0_A3;
            layer.setStyle(getStyle(layer.feature, !!visits[code]));
          });
        }
      })
      .catch(function (err) {
        console.error("Load visits error:", err);
      });
  }

  // ---- Stats ----
  function updateStats() {
    var count = Object.keys(visits).length;
    var total = 195;
    var pct = Math.round((count / total) * 100);
    var el = document.getElementById("legend-stats");
    if (el) {
      el.textContent = count + " / " + total + " countries (" + pct + "%)";
    }
  }

  // ---- Load GeoJSON ----
  fetch("/data/countries.geojson")
    .then(function (res) {
      return res.json();
    })
    .then(function (geojson) {
      geoLayer = L.geoJSON(geojson, {
        style: function (feature) {
          return getStyle(feature, false);
        },
        onEachFeature: onEachFeature,
      }).addTo(map);

      // Load visits after GeoJSON is ready
      loadVisits();
    })
    .catch(function (err) {
      console.error("GeoJSON load error:", err);
    });

  // ---- Utility ----
  function escapeHtml(str) {
    if (!str) return "";
    return str
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }
});
